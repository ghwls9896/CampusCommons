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
         'answer_type': ['multiple_choice', 'short_answer', 'written'][i-1],
         'options': ['가', '나', '다', '라'] if i == 1 else [], 'correct_index': 0 if i == 1 else -1,
         'model_answer': '가' if i == 1 else '자료의 핵심 개념', 'grading_criteria': [] if i == 1 else ['핵심 개념의 의미를 설명'],
         'explanation': '제시된 자료의 개념에 따른 해설입니다.',
         'source_page': i, 'source_quote': '자료의 근거 문장'}
        for i in (1, 2, 3)]}


def test_pdf_validation_rejects_non_pdf_encrypted_and_page_limit():
    assert validate_pdf(pdf()) == 3
    assert validate_pdf(pdf(81)) == 81
    for data, code in [(b'not a pdf', 'invalid_pdf'),
                       (pdf(encrypted=True), 'encrypted_pdf'),
                       (pdf(0), 'invalid_pdf')]:
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


def client_for(generator=None, grader=None, **overrides):
    config = dict(api_key='secret', access_token='team-code', daily_limit=2, max_pdf_bytes=1024 * 1024)
    config.update(overrides)
    async def default_generator(data, pages, *args):
        return validate_quiz(result(), pages)
    async def default_grader(answers, *args):
        return {'grades': [{'level': a.level, 'correct': bool(a.answer.strip()), 'feedback': '핵심 의미 확인'} for a in answers]}
    return TestClient(create_app(Settings(**config), generator or default_generator, grader or default_grader))


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
        for pages, allowed in [(1, True), (80, True), (81, True), (1201, True)]:
            data = pdf(pages)
            response = client.post('/v1/pdfs/inspect', content=data, headers=HEADERS)
            assert response.status_code == 200
            assert response.json() == {
                'size_bytes': len(data), 'page_count': pages,
                'max_size_bytes': 1024 * 1024, 'max_pages': 1000,
                'max_selected_pages': 1000, 'supports_page_ranges': True,
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


def test_range_sends_only_selected_pages_and_maps_original_source_numbers():
    from pypdf import PdfReader
    writer = PdfWriter()
    for i in range(120):
        writer.add_blank_page(width=300+i, height=300)
    out = io.BytesIO(); writer.write(out)
    calls = []
    async def generate(data, pages, *args):
        reader = PdfReader(io.BytesIO(data))
        calls.append((pages, [float(p.mediabox.width) for p in reader.pages]))
        payload = result()
        for question in payload['questions']: question['source_page'] = 2
        return validate_quiz(payload, pages)
    with client_for(generate) as client:
        response = client.post('/v1/quizzes?start_page=81&end_page=83', content=out.getvalue(), headers=HEADERS)
    assert response.status_code == 200
    assert calls == [(3, [380, 381, 382])]
    assert [q['source_page'] for q in response.json()['questions']] == [82, 82, 82]
    assert response.json()['page_range'] == {'start_page': 81, 'end_page': 83, 'total_pages': 120}


@pytest.mark.parametrize('query', [
    'start_page=0&end_page=3', 'start_page=3&end_page=2',
    'start_page=1&end_page=4', 'start_page=1', 'end_page=3',
    'start_page=oops&end_page=3', 'start_page=1.5&end_page=3',
])
def test_invalid_ranges_never_call_provider_or_spend_budget(query):
    with client_for(daily_limit=1) as client:
        response = client.post('/v1/quizzes?'+query, content=pdf(), headers=HEADERS)
        assert response.status_code == 422
        assert response.json()['error']['code'] == 'invalid_page_range'
        assert client.post('/v1/quizzes', content=pdf(), headers=HEADERS).status_code == 200


def test_whole_pdf_over_80_is_accepted_and_range_request_cap_is_explicit():
    with client_for(daily_limit=2) as client:
        assert client.post('/v1/quizzes', content=pdf(81), headers=HEADERS).status_code == 200
        response = client.post('/v1/quizzes', content=pdf(1001), headers=HEADERS)
        assert response.status_code == 422
        assert response.json()['error']['code'] == 'page_range_too_large'
        response = client.post('/v1/quizzes?start_page=999&end_page=1001', content=pdf(1001), headers=HEADERS)
        assert response.status_code == 200


@pytest.mark.parametrize('mutate', [
    lambda d: d['questions'][1].update(answer_type='multiple_choice'),
    lambda d: d['questions'][2].update(options=['a','b','c','d']),
    lambda d: d['questions'][1].update(model_answer=' '),
    lambda d: d['questions'][2].update(correct_index=0),
])
def test_mixed_question_contract_rejects_wrong_format(mutate):
    payload=result(); mutate(payload)
    with pytest.raises(QuizError): validate_quiz(payload, 3)


def submissions():
    return {'answers': [
        {'level': 2, 'prompt': '큐의 처리 순서는?', 'model_answer': '선입선출', 'grading_criteria': ['먼저 온 요청부터 처리'], 'answer': '먼저 들어온 것이 먼저 나온다'},
        {'level': 3, 'prompt': '스택과 큐를 비교하세요.', 'model_answer': '스택은 후입선출, 큐는 선입선출', 'grading_criteria': ['스택 후입선출', '큐 선입선출'], 'answer': '둘 다 항상 같은 순서로 처리한다'},
    ]}


def test_grade_endpoint_auth_validation_and_semantic_result():
    data=submissions()
    async def grade(answers,*args):
        assert [a.level for a in answers] == [2,3]
        return {'grades': [{'level': 2, 'correct': True, 'feedback': '같은 의미입니다.'}, {'level': 3, 'correct': False, 'feedback': '처리 순서가 다릅니다.'}]}
    with client_for(grader=grade) as client:
        assert client.post('/v1/quizzes/grade', json=data).status_code == 401
        response=client.post('/v1/quizzes/grade',json=data,headers={'X-Quiz-Token':'team-code'})
        assert response.status_code == 200
        assert [g['correct'] for g in response.json()['grades']] == [True,False]
        assert response.headers['cache-control'] == 'no-store'
        data['answers'][1]['level']=2
        assert client.post('/v1/quizzes/grade',json=data,headers={'X-Quiz-Token':'team-code'}).status_code == 422


def test_blank_grading_needs_no_provider_and_ai_grading_shares_budget():
    async def must_not_grade(*args): pytest.fail('blank answers need no AI call')
    blank=submissions()
    for answer in blank['answers']: answer['answer']=' '
    with client_for(grader=must_not_grade,daily_limit=1) as client:
        response=client.post('/v1/quizzes/grade',json=blank,headers={'X-Quiz-Token':'team-code'})
        assert response.status_code==200
        assert [g['correct'] for g in response.json()['grades']] == [False,False]
        assert client.post('/v1/quizzes',content=pdf(),headers=HEADERS).status_code==200
        assert client.post('/v1/quizzes/grade',json=submissions(),headers={'X-Quiz-Token':'team-code'}).status_code==429


def test_semantic_grader_provider_contract_and_empty_answer_override():
    from quiz import AnswerSubmission, grade_answers
    data=submissions(); data['answers'][1]['answer']=''
    answers=[AnswerSubmission.model_validate(a) for a in data['answers']]
    def upstream(request):
        body=json.loads(request.content)
        assert 'inlineData' not in str(body)
        assert '모범답안' in body['systemInstruction']['parts'][0]['text']
        assert body['generationConfig']['responseFormat']['text']['schema']['type']=='object'
        return httpx.Response(200,json={'candidates':[{'finishReason':'STOP','content':{'parts':[{'text':json.dumps({'grades':[{'level':2,'correct':True,'feedback':'동일한 의미입니다.'},{'level':3,'correct':True,'feedback':'제출 성공'}]})}]}}]})
    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(upstream)) as client:
            return await grade_answers(answers,'secret','gemini-3.5-flash-lite',client)
    assert [g['correct'] for g in asyncio.run(run())['grades']]==[True,False]


def test_invalid_grade_and_grading_timeout_release_busy_slot():
    async def invalid(*args):
        return {'grades':[{'level':2,'correct':True,'feedback':'ok'},{'level':2,'correct':True,'feedback':'ok'}]}
    with client_for(grader=invalid) as client:
        assert client.post('/v1/quizzes/grade',json=submissions(),headers={'X-Quiz-Token':'team-code'}).status_code==502
        assert client.post('/v1/pdfs/inspect',content=pdf(),headers=HEADERS).status_code==200
    async def slow(*args): await asyncio.sleep(.1)
    with client_for(grader=slow,grading_timeout=.01) as client:
        assert client.post('/v1/quizzes/grade',json=submissions(),headers={'X-Quiz-Token':'team-code'}).status_code==504
        assert client.post('/v1/pdfs/inspect',content=pdf(),headers=HEADERS).status_code==200


@pytest.mark.parametrize("kind", ["link", "article"])
def test_range_excludes_cross_page_link_targets_not_just_page_tree_entries(kind):
    from pypdf.annotations import Link
    from pypdf.generic import DecodedStreamObject, NameObject, DictionaryObject, ArrayObject, FloatObject
    from quiz import select_pdf_range
    writer=PdfWriter()
    for i in range(3):
        page=writer.add_blank_page(width=300,height=300)
        content=DecodedStreamObject();content.set_data(f'BT (PAGE_MARKER_{i}) Tj ET'.encode())
        page[NameObject('/Contents')]=writer._add_object(content)
    if kind == 'link':
        writer.add_annotation(page_number=0,annotation=Link(rect=(1,1,10,10),target_page_index=2))
    else:
        beads=[DictionaryObject({NameObject('/Type'):NameObject('/Bead'),NameObject('/P'):writer.pages[i].indirect_reference,
            NameObject('/R'):ArrayObject([FloatObject(n) for n in (0,0,300,300)])}) for i in (0,2)]
        b0,b2=[writer._add_object(b) for b in beads]
        thread=writer._add_object(DictionaryObject({NameObject('/F'):b0}))
        for bead,next_bead in zip(beads,(b2,b0)):
            bead[NameObject('/N')]=next_bead;bead[NameObject('/V')]=next_bead;bead[NameObject('/T')]=thread
        writer.pages[0][NameObject('/B')]=ArrayObject([b0]);writer.pages[2][NameObject('/B')]=ArrayObject([b2])
        writer._root_object[NameObject('/Threads')]=ArrayObject([thread])
    out=io.BytesIO();writer.write(out)
    selected,total,first,last=select_pdf_range(out.getvalue(),'1','1')
    assert (total,first,last)==(3,1,1)
    assert b'PAGE_MARKER_0' in selected
    assert b'PAGE_MARKER_1' not in selected
    assert b'PAGE_MARKER_2' not in selected


def test_range_keeps_visible_selected_page_notes():
    from pypdf import PdfReader
    from pypdf.annotations import FreeText
    from pypdf.generic import DecodedStreamObject, DictionaryObject, NameObject
    from quiz import select_pdf_range
    writer=PdfWriter()
    writer.add_blank_page(width=300,height=300);writer.add_blank_page(width=300,height=300)
    writer.add_annotation(page_number=0,annotation=FreeText(text='Selected study note',rect=(10,10,250,100)))
    appearance=DecodedStreamObject();appearance.set_data(b'NOTE_APPEARANCE_STREAM')
    note=writer.pages[0]['/Annots'][0].get_object()
    note[NameObject('/AP')]=DictionaryObject({NameObject('/N'):writer._add_object(appearance)})
    out=io.BytesIO();writer.write(out)
    selected,*_=select_pdf_range(out.getvalue(),'1','1')
    page=PdfReader(io.BytesIO(selected)).pages[0]
    assert len(page['/Annots'])==1
    assert page['/Annots'][0].get_object()['/Contents']=='Selected study note'

    assert page['/Annots'][0].get_object()['/AP']['/N'].get_data()==b'NOTE_APPEARANCE_STREAM'


def test_range_keeps_shared_note_appearance_resource_shared():
    from pypdf import PdfReader
    from pypdf.generic import DecodedStreamObject,DictionaryObject,NameObject,ArrayObject,FloatObject
    from quiz import select_pdf_range
    writer=PdfWriter();writer.add_blank_page(width=300,height=300);writer.add_blank_page(width=300,height=300)
    appearance=DecodedStreamObject();appearance.set_data(b'x'*(128*1024))
    ref=writer._add_object(appearance)
    for _ in range(81):
        note=DictionaryObject({NameObject('/Type'):NameObject('/Annot'),NameObject('/Subtype'):NameObject('/Stamp'),
             NameObject('/Rect'):ArrayObject([FloatObject(n) for n in (1,1,10,10)]),NameObject('/AP'):DictionaryObject({NameObject('/N'):ref})})
        writer.add_annotation(0,note)
    out=io.BytesIO();writer.write(out)
    selected,*_=select_pdf_range(out.getvalue(),'1','1')
    reader=PdfReader(io.BytesIO(selected));notes=reader.pages[0]['/Annots']
    first=notes[0].get_object()['/AP'].raw_get('/N')
    assert all(note.get_object()['/AP'].raw_get('/N')==first for note in notes)
    assert len(selected)<1024*1024
