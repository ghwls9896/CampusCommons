import asyncio
import copy
import io
import json

import httpx
import pytest
from fastapi.testclient import TestClient
from pypdf import PdfWriter

from quiz import QuizError, validate_pdf, validate_quiz, generate_quiz
from app import Settings, create_app


def pdf(pages=3, encrypted=False):
    writer = PdfWriter()
    for _ in range(pages):
        writer.add_blank_page(width=300, height=300)
    if encrypted:
        writer.encrypt('password')
    out = io.BytesIO()
    writer.write(out)
    return out.getvalue()


def result():
    return {'enough_material': True, 'reason': '', 'questions': [
        {'level': i, 'prompt': f'레벨 {i}의 문제는?',
         'options': ['가', '나', '다', '라'], 'correct_index': i - 1,
         'explanation': '제시된 자료의 개념에 따른 해설입니다.',
         'source_page': i, 'source_quote': '자료의 근거 문장'}
        for i in (1, 2, 3)]}


def test_pdf_validation_rejects_non_pdf_encrypted_and_page_limit():
    assert validate_pdf(pdf()) == 3
    for data, code in [(b'not a pdf', 'invalid_pdf'),
                       (pdf(encrypted=True), 'encrypted_pdf'),
                       (pdf(81), 'too_many_pages'), (pdf(0), 'invalid_pdf')]:
        with pytest.raises(QuizError) as error:
            validate_pdf(data)
        assert error.value.code == code


@pytest.mark.parametrize('mutate', [
    lambda d: d['questions'].pop(),
    lambda d: d['questions'][2].update(level=1),
    lambda d: d['questions'][0].update(correct_index=4),
    lambda d: d['questions'][0].update(source_page=4),
    lambda d: d['questions'][0].update(source_page=0),
    lambda d: d['questions'][0].update(explanation=' '),
    lambda d: d['questions'][0].update(options=['가', '가', '다', '라']),
    lambda d: d['questions'][0].update(level='1'),
    lambda d: d['questions'][0].update(source_quote=''),
])
def test_rejects_invalid_model_output(mutate):
    data = result()
    mutate(data)
    with pytest.raises(QuizError):
        validate_quiz(data, 3)


def test_valid_output_is_sorted_and_insufficient_material_is_explicit():
    data = result()
    data['questions'].reverse()
    assert [q['level'] for q in validate_quiz(data, 3)['questions']] == [1, 2, 3]
    with pytest.raises(QuizError) as error:
        validate_quiz({'enough_material': False, 'reason': '빈 문서', 'questions': []}, 3)
    assert error.value.code == 'insufficient_material'


def test_gemini_contract_sends_pdf_and_schema_without_key_in_url():
    def upstream(request):
        assert request.url.path.endswith('/models/gemini-3.8-flash:generateContent')
        assert 'secret' not in str(request.url)
        assert request.headers['x-goog-api-key'] == 'secret'
        body = json.loads(request.content)
        assert body['contents'][0]['parts'][0]['inlineData']['mimeType'] == 'application/pdf'
        assert body['generationConfig']['responseFormat']['text']['schema']['type'] == 'object'
        assert body['generationConfig'].get('thinkingConfig', {}).get('thinkingLevel') == 'low'
        return httpx.Response(200, json={'candidates': [{'finishReason': 'STOP', 'content': {
            'parts': [{'text': json.dumps(result())}]}}]})

    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(upstream)) as client:
            return await generate_quiz(pdf(), 3, 'secret', 'gemini-3.8-flash', client)
    assert len(asyncio.run(run())['questions']) == 3


@pytest.mark.parametrize('status,payload,code', [
    (429, {}, 'quota_exceeded'), (403, {}, 'provider_access'),
    (404, {}, 'model_unavailable'), (503, {}, 'provider_unavailable'),
    (200, {'candidates': []}, 'invalid_generation'),
    (200, {'candidates': [{'finishReason': 'MAX_TOKENS', 'content': {'parts': [{'text': '{}'}]}}]}, 'invalid_generation'),
    (200, {'candidates': [{'finishReason': 'STOP', 'content': {'parts': [{'text': 'not json'}]}}]}, 'invalid_generation'),
])
def test_provider_errors_do_not_become_fake_questions(status, payload, code):
    calls = []
    def upstream(request):
        calls.append(request)
        return httpx.Response(status, json=payload)
    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(upstream)) as client:
            return await generate_quiz(pdf(), 3, 'secret', 'gemini-3.8-flash', client)
    with pytest.raises(QuizError) as error:
        asyncio.run(run())
    assert error.value.code == code
    assert len(calls) == 1  # no hidden retries against free quota


def client_for(generator=None, **overrides):
    config = dict(api_key='secret', access_token='team-code', daily_limit=2, max_pdf_bytes=1024 * 1024)
    config.update(overrides)
    async def default_generator(data, pages, *args):
        return validate_quiz(result(), pages)
    return TestClient(create_app(Settings(**config), generator or default_generator))


HEADERS = {'content-type': 'application/pdf', 'x-quiz-token': 'team-code'}


def test_health_auth_upload_and_daily_limit():
    with client_for() as client:
        assert client.get('/healthz').json() == {'status': 'ok'}
        assert client.post('/v1/quizzes', content=pdf()).status_code == 401
        assert client.post('/v1/quizzes', content=b'bad', headers=HEADERS).status_code == 422
        assert client.post('/v1/quizzes', content=pdf(), headers=HEADERS).status_code == 200
        assert client.post('/v1/quizzes', content=pdf(), headers=HEADERS).status_code == 200
        r = client.post('/v1/quizzes', content=pdf(), headers=HEADERS)
        assert r.status_code == 429
        assert r.json()['error']['code'] == 'daily_limit'
        assert 'secret' not in r.text


def test_upload_size_and_content_type_are_checked_before_generation():
    with client_for(max_pdf_bytes=20) as client:
        assert client.post('/v1/quizzes', content=pdf(), headers=HEADERS).status_code == 413
    with client_for() as client:
        r = client.post('/v1/quizzes', content=pdf(), headers={**HEADERS, 'content-type': 'text/plain'})
        assert r.status_code == 415


def test_missing_key_and_access_code_fail_closed():
    for settings in ({'api_key': ''}, {'access_token': ''}):
        with client_for(**settings) as client:
            assert client.get('/healthz').status_code == 200
            assert client.post('/v1/quizzes', content=pdf(), headers=HEADERS).status_code == 503


def test_upstream_quota_is_presented_to_the_client():
    async def fail(*args):
        raise QuizError('quota_exceeded', '무료 한도를 초과했습니다.', 429)
    with client_for(fail) as client:
        r = client.post('/v1/quizzes', content=pdf(), headers=HEADERS)
        assert r.status_code == 429
        assert r.json()['error']['code'] == 'quota_exceeded'


def test_concurrent_generation_is_rejected_without_second_provider_call():
    calls = []
    async def run():
        started, finish = asyncio.Event(), asyncio.Event()
        async def slow(*args):
            calls.append(1)
            started.set()
            await finish.wait()
            return validate_quiz(result(), 3)
        app = create_app(Settings(api_key='secret', access_token='team-code'), slow)
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url='http://test') as client:
            first = asyncio.create_task(client.post('/v1/quizzes', content=pdf(), headers=HEADERS))
            await started.wait()
            second = await client.post('/v1/quizzes', content=pdf(), headers=HEADERS)
            finish.set()
            assert (await first).status_code == 200
            assert second.status_code == 429
    asyncio.run(run())
    assert len(calls) == 1


def test_slow_upload_times_out_and_releases_busy_slot():
    async def run():
        async def slow_body():
            yield b'%PDF-'
            await asyncio.sleep(.1)
        async def generate(data, pages, *args):
            return validate_quiz(result(), pages)
        app = create_app(Settings(api_key='secret', access_token='team-code', upload_timeout=.01), generate)
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url='http://test') as client:
            timed_out = await client.post('/v1/quizzes', content=slow_body(), headers=HEADERS)
            assert timed_out.status_code == 408
            assert timed_out.json()['error']['code'] == 'upload_timeout'
            assert (await client.post('/v1/quizzes', content=pdf(), headers=HEADERS)).status_code == 200
    asyncio.run(run())


def test_generation_deadline_releases_slot_after_timeout():
    async def slow(*args):
        await asyncio.sleep(.1)
    with client_for(slow, generation_timeout=.01) as client:
        for _ in range(2):
            response = client.post('/v1/quizzes', content=pdf(), headers=HEADERS)
            assert response.status_code == 504
            assert response.json()['error']['code'] == 'generation_timeout'


def test_inspection_reports_real_pages_including_over_limit_without_generating():
    async def must_not_generate(*args):
        pytest.fail('PDF inspection must not invoke Gemini')
    with client_for(must_not_generate) as client:
        for pages, allowed in [(1, True), (80, True), (81, False)]:
            data = pdf(pages)
            response = client.post('/v1/pdfs/inspect', content=data, headers=HEADERS)
            assert response.status_code == 200
            assert response.json() == {
                'size_bytes': len(data), 'page_count': pages,
                'max_size_bytes': 1024 * 1024, 'max_pages': 80,
                'within_limits': allowed,
            }
            assert response.headers['cache-control'] == 'no-store'


def test_inspection_checks_auth_type_size_and_pdf_structure():
    with client_for(max_pdf_bytes=1024 * 1024) as client:
        assert client.post('/v1/pdfs/inspect', content=pdf()).status_code == 401
        assert client.post('/v1/pdfs/inspect', content=pdf(), headers={**HEADERS, 'content-type': 'text/plain'}).status_code == 415
        for data, code in [(b'broken', 'invalid_pdf'), (pdf(encrypted=True), 'encrypted_pdf'), (pdf(0), 'invalid_pdf')]:
            response = client.post('/v1/pdfs/inspect', content=data, headers=HEADERS)
            assert response.status_code == 422
            assert response.json()['error']['code'] == code
    with client_for(max_pdf_bytes=20) as client:
        assert client.post('/v1/pdfs/inspect', content=pdf(), headers=HEADERS).status_code == 413


def test_inspection_does_not_spend_generation_budget():
    with client_for(daily_limit=1) as client:
        for _ in range(3):
            assert client.post('/v1/pdfs/inspect', content=pdf(), headers=HEADERS).status_code == 200
        assert client.post('/v1/quizzes', content=pdf(), headers=HEADERS).status_code == 200
        assert client.post('/v1/quizzes', content=pdf(), headers=HEADERS).status_code == 429
        assert client.post('/v1/pdfs/inspect', content=pdf(), headers=HEADERS).status_code == 200
