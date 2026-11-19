import base64
from contextlib import ExitStack, contextmanager
import json
from pathlib import Path
import shlex
import socket as tcp
import threading
import time
from uuid import UUID

import httpx
import pytest
import uvicorn
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect
from websockets.exceptions import ConnectionClosed
from websockets.sync.client import connect

from app import create_app


class Accounts:
    """Only the upstream account service is simulated. Tokens are local fixtures."""

    def __init__(self):
        self.profiles = {}
        self.calls = 0

    def issue(self, number, expires=None):
        payload = {'sub': str(UUID(int=number)), 'exp': expires if expires is not None else int(time.time()) + 120}
        body = base64.urlsafe_b64encode(json.dumps(payload).encode()).decode().rstrip('=')
        token = f'fixture.{body}.fixture'
        self.profiles[token] = {'user_id': payload['sub'], 'nickname': '학생', 'level': 1}
        return token

    def handle(self, request):
        self.calls += 1
        assert request.method == 'POST'
        assert str(request.url) == 'https://accounts.example.test/functions/v1/account'
        assert request.headers['apikey'] == 'sb_publishable_test'
        assert json.loads(request.content) == {'action': 'read'}
        token = request.headers['authorization'].removeprefix('Bearer ')
        if token not in self.profiles:
            return httpx.Response(401)
        return httpx.Response(200, json=self.profiles[token])


@pytest.fixture
def accounts(monkeypatch):
    monkeypatch.setenv('SUPABASE_URL', 'https://accounts.example.test')
    monkeypatch.setenv('SUPABASE_PUBLISHABLE_KEY', 'sb_publishable_test')
    return Accounts()


@pytest.fixture
def live_lobby(accounts):
    app = create_app(lobby_account_transport=httpx.MockTransport(accounts.handle))
    blueprint = (Path(__file__).resolve().parents[2] / 'render.yaml').read_text()
    start = next(line for line in blueprint.splitlines() if line.strip().startswith('startCommand:'))
    flags = shlex.split(start.split(':', 1)[1])
    assert flags[flags.index('--workers') + 1] == '1'
    message_limit = int(flags[flags.index('--ws-max-size') + 1])
    assert message_limit == 1024
    with tcp.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        host, port = listener.getsockname()
        server = uvicorn.Server(uvicorn.Config(app, ws_max_size=message_limit, log_level='error'))
        thread = threading.Thread(target=server.run, kwargs={'sockets': [listener]}, daemon=True)
        thread.start()
        try:
            deadline = time.monotonic() + 5
            while not server.started:
                assert thread.is_alive() and time.monotonic() < deadline, 'Uvicorn startup timed out'
                time.sleep(.01)
            yield f'ws://{host}:{port}/v1/lobby/ws'
        finally:
            server.should_exit = True
            thread.join(timeout=5)
            assert not thread.is_alive()


def dial(url, token):
    return connect(url, additional_headers={'Authorization': 'Bearer ' + token},
                   proxy=None, open_timeout=3, close_timeout=1)


@contextmanager
def join(url, token):
    with dial(url, token) as peer:
        welcome = json.loads(peer.recv(timeout=3))
        assert welcome['type'] == 'welcome'
        yield peer, welcome


def wait_roster(peer, condition):
    deadline = time.monotonic() + 3
    while True:
        frame = json.loads(peer.recv(timeout=max(0, deadline - time.monotonic())))
        assert frame['type'] == 'snapshot'
        if condition(frame['players']):
            return frame['players']


def assert_close(peer, code, reason):
    with pytest.raises(ConnectionClosed) as failure:
        while True:
            peer.recv(timeout=3)
    assert (failure.value.rcvd.code, failure.value.rcvd.reason) == (code, reason)


MOVE = {'type': 'move', 'seq': 2, 'x': 2, 'y': .08, 'z': -3, 'yaw': 90, 'motion': 'walk'}


def test_lobby_requires_account_even_when_quiz_can_start():
    with TestClient(create_app()) as client:
        assert client.get('/healthz').json() == {'status': 'ok'}
        with pytest.raises(WebSocketDisconnect) as failure:
            with client.websocket_connect('/v1/lobby/ws') as socket:
                socket.receive_json()
        assert (failure.value.code, failure.value.reason) == (4401, 'unauthorized')


def test_two_clients_and_late_join_receive_current_public_roster(live_lobby, accounts):
    first, second, third = (accounts.issue(n) for n in (1, 2, 3))
    accounts.profiles[second]['level'] = 15
    with join(live_lobby, first) as (a, own):
        aid = own['self_id']
        assert own['players'] == [{'id': aid, 'nickname': '학생', 'level': 1, 'slot': 0, 'pose': None}]
        with join(live_lobby, second) as (b, other):
            bid = other['self_id']
            assert aid != bid and {p['id'] for p in other['players']} == {aid, bid}
            assert len({p['slot'] for p in other['players']}) == 2
            roster = wait_roster(a, lambda players: len(players) == 2)
            assert {p['id']: p['level'] for p in roster} == {aid: 1, bid: 15}
            a.send(json.dumps(MOVE))
            a.send(json.dumps({**MOVE, 'seq': 1, 'x': 99}))
            expected_pose = {k: v for k, v in MOVE.items() if k != 'type'}
            seen = wait_roster(b, lambda players: any(p['pose'] for p in players))
            assert [p['pose'] for p in seen if p['id'] == aid] == [expected_pose]
            assert [p['pose'] for p in seen if p['id'] == bid] == [None]
            with join(live_lobby, third) as (_, late):
                assert [p['pose'] for p in late['players'] if p['id'] == aid] == [expected_pose]
        remaining = wait_roster(a, lambda players: len(players) == 1)
        assert remaining[0]['id'] == aid
    assert accounts.calls == 3


def test_eight_seats_replacement_and_first_player_departure(live_lobby, accounts):
    tokens = [accounts.issue(n) for n in range(1, 10)]
    with ExitStack() as active:
        occupants = [active.enter_context(join(live_lobby, token)) for token in tokens[:8]]
        assert len(occupants[-1][1]['players']) == 8
        with dial(live_lobby, tokens[8]) as ninth:
            assert_close(ninth, 4409, 'lobby_full')
        replacement, welcome = active.enter_context(join(live_lobby, tokens[0]))
        assert len(welcome['players']) == 8
        assert welcome['self_id'] != occupants[0][1]['self_id']
        assert_close(occupants[0][0], 4400, 'replaced')
        replacement.send(json.dumps(MOVE))
        survivor = occupants[1][0]
        wait_roster(survivor, lambda roster: any(p['id'] == welcome['self_id'] and p['pose'] for p in roster))
        replacement.close()
        wait_roster(survivor, lambda roster: len(roster) == 7)
        with join(live_lobby, tokens[8]) as (_, accepted):
            assert len(accepted['players']) == 8
            assert next(p['slot'] for p in accepted['players'] if p['id'] == accepted['self_id']) == 0


@pytest.mark.parametrize('change', [
    {'seq': -1}, {'level': 12}, {'x': float('nan')}, {'z': True}, {'motion': 'unknown'}, {'id': 'other'},
])
def test_invalid_move_only_removes_its_sender(live_lobby, accounts, change):
    with join(live_lobby, accounts.issue(1)) as (a, own), join(live_lobby, accounts.issue(2)) as (b, _):
        b.send(json.dumps({**MOVE, **change}))
        assert_close(b, 1008, 'invalid_move')
        remaining = wait_roster(a, lambda players: len(players) == 1)
        assert remaining == own['players']


def test_token_expiry_and_denial_never_keep_seats(live_lobby, accounts):
    with dial(live_lobby, 'unrecognized') as denied:
        assert_close(denied, 4401, 'unauthorized')
    with join(live_lobby, accounts.issue(10, int(time.time()) + 2)) as (expiring, _):
        assert_close(expiring, 4401, 'token_expired')
    with join(live_lobby, accounts.issue(11)) as (_, after):
        assert len(after['players']) == 1 and after['players'][0]['slot'] == 0


@pytest.mark.parametrize('response', [
    httpx.Response(503), httpx.Response(429), httpx.Response(403), httpx.Response(200, text='unavailable'),
    httpx.Response(200, json={'nickname': '학생', 'user_id': 'bad'}),
    *(httpx.Response(200, json={'nickname': '학생', 'user_id': str(UUID(int=1)), **extra})
      for extra in ({}, {'level': 0}, {'level': 16}, {'level': True}, {'level': '12'})),
])
def test_account_service_errors_are_retryable_and_quiz_remains_available(accounts, response):
    transport = httpx.MockTransport(lambda _: response)
    with TestClient(create_app(lobby_account_transport=transport)) as client:
        with pytest.raises(WebSocketDisconnect) as failure:
            with client.websocket_connect('/v1/lobby/ws', headers={'Authorization': 'Bearer ' + accounts.issue(1)}) as ws:
                ws.receive_json()
        assert (failure.value.code, failure.value.reason) == (1013, 'account_unavailable')
        assert client.get('/healthz').is_success


def test_all_eight_move_and_stationary_roster_is_quiet(live_lobby, accounts):
    with ExitStack() as active:
        peers = [active.enter_context(join(live_lobby, accounts.issue(n))) for n in range(1, 9)]
        for seq in range(3):
            for slot, (peer, _) in enumerate(peers):
                peer.send(json.dumps({**MOVE, 'seq': seq, 'x': slot}))
            time.sleep(.1)
        for peer, _ in peers:
            roster = wait_roster(peer, lambda players: len(players) == 8 and
                                 all(p['pose'] and p['pose']['seq'] == 2 for p in players))
            assert all(p['pose']['x'] == p['slot'] for p in roster)
        with pytest.raises(TimeoutError):
            peers[0][0].recv(timeout=.25)
        assert accounts.calls == 8


def test_binary_frame_and_transport_size_limit(live_lobby, accounts):
    with join(live_lobby, accounts.issue(1)) as (peer, _):
        peer.send(b'{}')
        assert_close(peer, 1003, 'text_required')
    with join(live_lobby, accounts.issue(2)) as (peer, _):
        peer.send('a' * 1025)
        with pytest.raises(ConnectionClosed) as closed:
            peer.recv(timeout=3)
        assert closed.value.rcvd.code == 1009


def test_unconfigured_or_incomplete_profile_cannot_join(accounts, monkeypatch):
    token = accounts.issue(1)
    accounts.profiles[token]['nickname'] = ''
    with TestClient(create_app(lobby_account_transport=httpx.MockTransport(accounts.handle))) as client:
        for expected in [(4403, 'profile_not_ready'), (1013, 'account_unavailable')]:
            with pytest.raises(WebSocketDisconnect) as failure:
                with client.websocket_connect('/v1/lobby/ws', headers={'Authorization': 'Bearer ' + token}) as ws:
                    ws.receive_json()
            assert (failure.value.code, failure.value.reason) == expected
            monkeypatch.delenv('SUPABASE_PUBLISHABLE_KEY', raising=False)
    assert accounts.calls == 1
