import asyncio
import json
import threading
import time
from types import SimpleNamespace

import httpx
import pytest
from fastapi.testclient import TestClient

import duel
from app import Settings, create_app
from quiz import QuizError, generate_quiz, validate_quiz
from test_lobby import Accounts
from test_quiz import pdf, result


class Game:
    def __init__(self, monkeypatch):
        monkeypatch.setenv('SUPABASE_URL', 'https://accounts.example.test')
        monkeypatch.setenv('SUPABASE_PUBLISHABLE_KEY', 'sb_publishable_test')
        self.now = 100.
        monkeypatch.setattr(duel, 'time', SimpleNamespace(monotonic=lambda: self.now, time=time.time))
        self.accounts = Accounts()
        self.credentials = [self.accounts.issue(n) for n in (1, 2, 3)]
        for token, level in zip(self.credentials, (2, 11, 15)):
            self.accounts.profiles[token]['level'] = level
        self.generated = []
        self.graded = []
        self.fail_generate_once = False
        self.fail_grade_once = False
        self.hold_generation = threading.Event()
        self.hold_generation.set()
        self.cancelled = threading.Event()
        self.material = pdf(4)
        self.tickets = {}
        self.daily_limit = 30

    def open(self):
        config = Settings(api_key='fixture', access_token='unused', daily_limit=self.daily_limit)
        app = create_app(config, self.generate, self.grade, lobby_account_transport=httpx.MockTransport(self.accounts.handle))
        self.client = TestClient(app)
        return self.client

    async def generate(self, data, pages, key, model, client, target_level):
        self.generated.append((pages, target_level))
        try:
            while not self.hold_generation.is_set():
                await asyncio.sleep(.01)
        except asyncio.CancelledError:
            self.cancelled.set()
            raise
        if self.fail_generate_once and pages == 4:
            self.fail_generate_once = False
            raise QuizError('provider_unavailable', 'AI 응답을 기다려 주세요.', 503)
        quiz = result()
        for q in quiz['questions']:
            q['prompt'] = 'PDF %s, 문제 %s' % (pages, q['level'])
        return validate_quiz(quiz, pages)

    async def grade(self, answers, *args):
        self.graded.append([a.answer for a in answers])
        if self.fail_grade_once:
            self.fail_grade_once = False
            raise QuizError('invalid_grading', '다시 채점해 주세요.', 503)
        return {'grades': [{'level': a.level, 'criterion_scores': [10 if a.answer == '정답' else 5],
                            'feedback': '채점했습니다.'} for a in answers]}

    def join(self, user, slot):
        response = self.client.post(f'/v1/duel/seats/{slot}', headers={'Authorization': 'Bearer ' + self.credentials[user]})
        if response.is_success:
            data = response.json()
            self.tickets[user] = {'Authorization': 'Bearer ' + data['ticket']}
            self.round_id = data['state']['round_id']
        return response

    def state(self, user):
        return self.client.get('/v1/duel/state', headers=self.tickets[user]).json()

    def wait(self, phase, user=0):
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            state = self.state(user)
            if state['phase'] == phase:
                return state
            time.sleep(.01)
        pytest.fail('phase %s not reached; last %r' % (phase, state))

    def upload(self, user, material=None, **query):
        return self.client.post('/v1/duel/pdf', params={'round_id': self.round_id, **query},
                                headers={**self.tickets[user], 'Content-Type': 'application/pdf'},
                                content=material if material is not None else self.material)

    def move(self, user, level, choice=-1, text='', round_id=None):
        return self.client.post('/v1/duel/answer', headers=self.tickets[user],
                                json={'round_id': round_id or self.round_id, 'level': level, 'choice': choice, 'text': text})

    def leave(self, user):
        return self.client.delete('/v1/duel/seat', headers=self.tickets[user])

    def retry(self):
        return self.client.post('/v1/duel/retry', headers=self.tickets[0], params={'round_id': self.round_id})

    def pair(self):
        assert self.join(0, 0).json()['state']['phase'] == 'waiting'
        assert self.join(1, 1).json()['state']['phase'] == 'upload'

    def prepare(self):
        self.pair()
        assert self.upload(0, start_page=1, end_page=3).is_success
        assert not self.generated
        assert self.upload(1).is_success


@pytest.fixture
def game(monkeypatch):
    return Game(monkeypatch)


def test_two_seats_use_private_pdfs_common_level_and_server_scored_results(game):
    with game.open():
        game.pair()
        assert game.upload(0, start_page=1, end_page=3).json()['phase'] == 'upload'
        assert game.state(1)['players'][0]['ready']
        assert not game.generated
        assert game.upload(1).is_success
        for user, pages in enumerate((3, 4)):
            state = game.wait('playing', user)
            assert state['target_level'] == 7 and state['self_slot'] == user
            assert state['remaining_ms'] == 10_000
            assert state['question'] == {'level': 1, 'prompt': f'PDF {pages}, 문제 1',
                                         'options': ['가','나','다','라'], 'answer_type': 'multiple_choice'}
            assert 'correct_index' not in json.dumps(state) and not state['results']
        assert game.generated == [(3, 7), (4, 7)]
        game.now += 2
        assert game.move(0, 1, choice=0).json()['question']['level'] == 2
        assert game.move(0, 1, choice=3).json()['question']['level'] == 2  # lost response/replay
        assert game.move(0, 3, text='건너뛰기').status_code == 409
        assert game.move(1, 1, choice=2).is_success
        game.now += 4
        assert game.move(0, 2, text='정답').is_success and game.move(1, 2, text='부분점수').is_success
        game.now += 3
        assert game.move(0, 3, text='정답').json()['phase'] == 'grading'
        assert game.state(1)['phase'] == 'playing' and game.state(0)['results'] == []
        game.now += 2
        assert game.move(1, 3, text='정답').is_success
        a = game.wait('results')
        assert a['results'] == game.state(1)['results']
        x, y = a['results']
        assert (x['elapsed_ms'], x['score'], x['correct'], x['accuracy']) == (9000, 30, 3, 100)
        assert (y['elapsed_ms'], y['score'], y['correct'], y['accuracy']) == (11000, 15, 1, 33.3)
        assert y['stages'] == [{'level': 1, 'score': 0, 'elapsed_ms': 2000},
                               {'level': 2, 'score': 5, 'elapsed_ms': 4000},
                               {'level': 3, 'score': 10, 'elapsed_ms': 5000}]
        assert game.leave(0).status_code == 204
        assert game.state(1)['results'] == a['results']
        assert game.leave(1).status_code == 204
        assert game.join(2, 0).json()['state']['phase'] == 'waiting'


def test_one_seat_denies_files_and_conflicts_rejoin_rotates_only_owner_ticket(game):
    with game.open():
        assert game.client.post('/v1/duel/seats/0').status_code == 401
        assert game.join(0, 0).is_success
        original = game.tickets[0]
        assert game.upload(0).status_code == 409
        assert game.join(1, 0).status_code == 409 and game.join(0, 1).status_code == 409
        assert game.join(0, 0).is_success and original != game.tickets[0]
        assert game.client.delete('/v1/duel/seat', headers=original).status_code == 401
        game.join(1, 1)
        assert game.join(2, 0).status_code == 409 and not game.generated
        assert game.upload(0, b'not pdf').status_code == 422
        assert not game.state(0)['players'][0]['ready']
        assert game.leave(1).status_code == 204
        old = game.round_id
        game.join(2, 1)
        assert old != game.round_id
        assert game.upload(0, round_id=old).status_code == 409
        assert game.move(0, 1, choice=2, round_id=old).status_code == 409


def test_deadlines_expire_without_client_answers_or_local_clock(game):
    with game.open():
        game.prepare()
        game.wait('playing')
        game.now += 10
        assert game.move(0, 1, choice=0).json()['question']['level'] == 2
        assert game.move(0, 2, text='임의' * 200).status_code == 422
        assert game.state(1)['question']['level'] == 2
        game.now += 15
        assert game.state(0)['question']['level'] == 3
        assert game.state(1)['remaining_ms'] == 20_000
        game.now += 20
        results = game.wait('results')['results']
        assert all(r['elapsed_ms'] == 45000 and r['score'] == 0 and r['correct'] == 0 for r in results)
        assert game.graded == []


def test_seat_lease_expiration_frees_chair_and_discards_private_files(game):
    with game.open():
        game.pair()
        game.upload(0)
        game.now += 30
        game.state(1)
        game.now += 16
        remaining = game.state(1)
        assert remaining['phase'] == 'waiting' and remaining['round_id'] != game.round_id
        assert game.client.get('/v1/duel/state', headers=game.tickets[0]).status_code == 401
        assert game.join(2, 0).is_success
        assert not any(player['ready'] for player in game.state(1)['players'])


def test_leaving_during_generation_cancels_old_job_before_seat_reuse(game):
    with game.open():
        game.hold_generation.clear()
        game.prepare()
        game.wait('generating')
        assert game.leave(0).status_code == 204
        assert game.cancelled.wait(2)
        remaining = game.state(1)
        assert remaining['phase'] == 'waiting'
        assert game.join(2, 0).json()['state']['round_id'] == remaining['round_id']
        assert not any(p['ready'] for p in game.state(1)['players'])


def test_retry_keeps_successful_pdf_and_reuses_answers_after_grading_failure(game):
    with game.open():
        game.fail_generate_once = True
        game.prepare()
        state = game.wait('error')
        assert state['message'] and state['can_upload']
        assert game.retry().is_success
        game.wait('playing')
        assert game.generated == [(3, 7), (4, 7), (4, 7)]
        game.fail_grade_once = True
        for user in (0,1):
            game.move(user, 1, choice=0)
            game.move(user, 2, text='정답')
            game.move(user, 3, text='정답')
        failed = game.wait('error')
        assert not failed['can_upload'] and game.upload(0).status_code == 409
        assert game.retry().is_success
        assert all(r['score'] == 30 for r in game.wait('results')['results'])


def test_duel_uses_same_daily_ai_budget_as_existing_quiz(game):
    game.daily_limit = 2
    with game.open():
        game.prepare()
        game.wait('playing')
        denied = game.client.post('/v1/quizzes', headers={'X-Quiz-Token': 'unused', 'Content-Type': 'application/pdf'},
                                  content=game.material)
        assert denied.status_code == 429 and denied.json()['error']['code'] == 'daily_limit'


def test_gemini_receives_shared_level_in_system_instruction_for_both_documents():
    requests = []
    def capture(request):
        requests.append(json.loads(request.content))
        return httpx.Response(200, json={'candidates': [{'finishReason': 'STOP', 'content': {'parts': [{'text': json.dumps(result())}]}}]})
    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(capture)) as client:
            for pages in (3, 4):
                await generate_quiz(pdf(pages), pages, 'fixture', 'test-model', client, 7)
    asyncio.run(run())
    first, second = requests
    assert first['systemInstruction'] == second['systemInstruction']
    assert '15단계 중 7' in first['systemInstruction']['parts'][0]['text']
    assert first['contents'] != second['contents']


def test_authoritative_timeout_grades_last_draft_and_reveals_only_own_review(game):
    def draft(user, level, choice=-1, text='', seq=None):
        payload = {'round_id': game.round_id, 'level': level, 'choice': choice, 'text': text}
        if seq is not None:
            payload['seq'] = seq
        return game.client.post('/v1/duel/draft', headers=game.tickets[user], json=payload)
    with game.open():
        game.pair()
        assert game.upload(0, start_page=2, end_page=4).is_success
        assert game.upload(1).is_success
        assert game.wait('playing')['review'] == []
        assert draft(0, 1, choice=0, seq=1).json()['question']['level'] == 1
        assert draft(1, 1, choice=0, seq=1).is_success
        assert draft(1, 1, choice=-1, seq=2).is_success  # clear the selection
        assert draft(1, 1, choice=0, seq=1).is_success  # delayed write cannot undo clear
        game.now += 10
        assert draft(0, 1, choice=2).json()['question']['level'] == 2
        assert draft(0, 2, text='정답', seq=2).is_success
        assert draft(0, 2, text='낡은 초안', seq=1).is_success
        assert draft(1, 2, text='다른 참가자의 답').is_success
        game.now += 15
        assert game.state(0)['question']['level'] == 3
        assert draft(0, 3, text='삭제할 답', seq=1).is_success
        assert draft(0, 3, text='', seq=2).is_success
        assert draft(1, 3, text='정답').is_success
        game.state(1)
        game.now += 20
        a = game.wait('results')
        b = game.state(1)
        assert a['results'][0]['score'] == 20 and a['results'][1]['score'] == 15
        assert a['results'] == b['results']
        assert [entry['score'] for entry in a['review']] == [10, 10, 0]
        assert [entry['source_page'] for entry in a['review']] == [2, 3, 4]
        assert [entry['source_page'] for entry in b['review']] == [1, 2, 3]
        assert [entry['answer'] for entry in a['review']] == ['가', '정답', '']
        assert [entry['answer'] for entry in b['review']] == ['', '다른 참가자의 답', '정답']
        assert '다른 참가자의 답' not in json.dumps(a, ensure_ascii=False)
        for entry in a['review']:
            assert 'PDF 3,' in entry['prompt'] and entry['model_answer'] and entry['explanation'] and entry['feedback']
        assert all('PDF 4,' in entry['prompt'] for entry in b['review'])
        game.leave(1)
        assert game.state(0)['review'] == a['review']


def test_legacy_ai_work_is_bounded_while_duel_uses_single_worker(game):
    with game.open():
        game.hold_generation.clear()
        game.prepare()
        deadline = time.monotonic() + 2
        while not game.generated:
            assert time.monotonic() < deadline
            time.sleep(.01)
        headers = {'X-Quiz-Token': 'unused', 'Content-Type':'application/pdf'}
        for path in ('/v1/pdfs/inspect', '/v1/quizzes'):
            response = game.client.post(path, headers=headers, content=game.material)
            assert response.status_code == 429 and response.json()['error']['code'] == 'busy'
        assert game.state(0)['phase'] == 'generating'  # heartbeat is independent of AI/PDF work
        game.hold_generation.set()
        game.wait('playing')
        assert game.client.post('/v1/pdfs/inspect', headers=headers, content=game.material).is_success
