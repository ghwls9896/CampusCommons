"""Exercise the local table and Render HTTP boundary without using AI quota."""
import asyncio
import json

import httpx
import pytest
from fastapi.testclient import TestClient

from app import Settings, create_app
from hosted_quiz import HostedQuiz
from quiz import QuizError, pdf_page_count, validate_quiz
from test_duel import Game
from test_quiz import client_for, pdf, result


class MockRender(HostedQuiz):
    def __init__(self, upgraded):
        super().__init__('fixture-demo-code')
        self.upgraded = upgraded
        self.requests = []
        self.busy_once = False

    def handle(self, request):
        assert request.url.scheme == 'https' and request.url.host == 'campuscommons-quiz.onrender.com'
        assert request.headers['X-Quiz-Token'] == 'fixture-demo-code'
        assert not {'Authorization', 'apikey', 'x-goog-api-key'}.intersection(request.headers)
        self.requests.append(request)
        if request.url.path.endswith('/grade'):
            answers = json.loads(request.content)['answers']
            return httpx.Response(200, json={'grades': [
                {'level': a['level'], 'criterion_scores': [10], 'feedback': '채점 완료'} for a in answers]})
        assert request.url.path == '/v1/quizzes' and request.headers['Content-Type'] == 'application/pdf'
        if self.busy_once and len(self.requests) == 2:
            return httpx.Response(429, json={'error': {'code': 'busy', 'message': 'sensitive data'}})
        pages = pdf_page_count(request.content)
        reply = validate_quiz(result(), pages)
        reply['page_range'] = {'start_page': 1, 'end_page': pages, 'total_pages': pages}
        if self.upgraded and 'target_level' in request.url.params:
            reply['target_level'] = int(request.url.params['target_level'])
        return httpx.Response(200, json=reply)

    async def _post(self, client, path, **kwargs):
        async with httpx.AsyncClient(transport=httpx.MockTransport(self.handle)) as mocked:
            return await super()._post(mocked, path, **kwargs)


@pytest.mark.parametrize('upgraded,mode', [(True, 'account'), (False, 'standard')])
def test_paired_render_mode_uses_no_local_key_and_protects_each_sheet(monkeypatch, upgraded, mode):
    game = Game(monkeypatch)
    provider = MockRender(upgraded)
    provider.busy_once = True
    accounts = httpx.MockTransport(game.accounts.handle)
    assert not Settings().api_key
    app = create_app(Settings(), hosted_quiz=provider, lobby_account_transport=accounts)
    with TestClient(app) as game.client:
        game.pair()
        assert game.state(0)['difficulty_mode'] == 'pending'
        game.upload(0, start_page=2, end_page=4)
        game.upload(1)
        assert '처리 중' in game.wait('error')['message']
        assert len(provider.requests) == 2
        game.retry()
        for i in (0, 1):
            state = game.wait('playing', i)
            assert state['difficulty_mode'] == mode
            assert state['target_level'] == 7 and not state['review']
            assert game.move(i, 1, choice=0).is_success
            assert game.move(i, 2, text='내 답변').is_success
            assert game.move(i, 3, text='긴 답변').is_success
        assert game.wait('results')['review'][0]['source_page'] == 2  # original selected range
        assert game.state(1)['review'][0]['source_page'] == 1
        assert len(provider.requests) == 5  # 2 generated, 1 failed, 2 grades
        requests = provider.requests[:3]
        assert [pdf_page_count(r.content) for r in requests] == [3, 4, 4]
        assert requests[0].url.params['target_level'] == '7'
        for request in requests[1:]:
            assert request.url.params.get('target_level') == ('7' if upgraded else None)


def test_legacy_endpoint_passes_requested_level_to_generator_and_echoes_it():
    levels = []

    async def generate(data, pages, key, model, client, *args):
        levels.append(args)
        return validate_quiz(result(), pages)

    with client_for(generator=generate) as api:
        headers = {'Content-Type': 'application/pdf', 'X-Quiz-Token': 'team-code'}
        for invalid in ('16', '0', 'abc'):
            assert api.post('/v1/quizzes', headers=headers, params={'target_level': invalid}, content=pdf()).status_code == 422
        assert not levels
        assert 'target_level' not in api.post('/v1/quizzes', headers=headers, content=pdf()).json()
        reply = api.post('/v1/quizzes', headers=headers, params={'target_level': 12}, content=pdf())
        assert reply.is_success and reply.json()['target_level'] == 12
        assert levels == [(), (12,)]


@pytest.mark.parametrize('token', ['', 'AIza-not-a-demo-code', 'AQ.provider', 'a\nb', 'a b', 'a' * 129])
def test_deployment_token_guard(token):
    with pytest.raises(ValueError):
        HostedQuiz(token)


def test_redirects_and_invalid_generation_are_never_replayed():
    provider = HostedQuiz('fixture-only')
    for response in [
        httpx.Response(307, headers={'Location': 'https://elsewhere.example'}),
        httpx.Response(200, text='invalid'),
        httpx.Response(200, json={'page_range': {'start_page': 1, 'end_page': 3, 'total_pages': 3}, 'questions': []}),
        httpx.Response(200, json={'page_range': {'start_page': 1, 'end_page': 3, 'total_pages': 3},
                                  'questions': result()['questions'], 'target_level': 9}),
    ]:
        seen = []
        def handle(request):
            seen.append(request)
            return response
        async def run():
            async with httpx.AsyncClient(transport=httpx.MockTransport(handle), follow_redirects=True) as client:
                await provider.generate(pdf(), 3, client, 7)
        with pytest.raises(QuizError):
            asyncio.run(run())
        assert len(seen) == 1 and seen[0].url.host == 'campuscommons-quiz.onrender.com'


def test_hosted_grade_rechecks_points_against_saved_rubric():
    from quiz import AnswerSubmission

    answers = [AnswerSubmission(level=i, prompt='문제', model_answer='정답', answer='학생 답',
                                grading_criteria=[{'description': '개념', 'max_points': 4},
                                                  {'description': '근거', 'max_points': 6}]) for i in (2, 3)]
    invalid = {'grades': [{'level': i, 'criterion_scores': [5, 5], 'feedback': '완료'} for i in (2, 3)]}

    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(lambda _: httpx.Response(200, json=invalid))) as client:
            await HostedQuiz('fixture-only').grade(answers, client)

    with pytest.raises(QuizError) as caught:
        asyncio.run(run())
    assert caught.value.code == 'invalid_grading'
