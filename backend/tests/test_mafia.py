import asyncio
import json
import time

import httpx
from fastapi.testclient import TestClient

from app import Settings, create_app
from mafia import MafiaRooms, ModalDialogue


def join_headers(token=None):
    headers = {"x-quiz-token": "test-room-access"}
    if token:
        headers["x-mafia-member"] = token
    return headers


def make_clients(dialogue):
    rooms = MafiaRooms(dialogue=dialogue)
    return TestClient(create_app(Settings(access_token="test-room-access"), mafia_rooms=rooms)), rooms


def create_room(client):
    response = client.post("/v1/mafia/rooms", json={"nickname": "진행자"}, headers=join_headers())
    assert response.status_code == 200, response.text
    created = response.json()
    route = "/v1/mafia/rooms/" + created["room_id"]
    guest = client.post(route + "/join", json={"nickname": "참가자"}, headers=join_headers())
    assert guest.status_code == 200, guest.text
    return route, join_headers(created["member_token"]), join_headers(guest.json()["member_token"])


def state(client, route, headers):
    response = client.get(route, headers=headers)
    assert response.status_code == 200, response.text
    return response.json()


def action(client, route, headers, kind, **extra):
    revision = state(client, route, headers)["revision"]
    return client.post(route + "/actions", headers=headers, json={"kind": kind, "revision": revision, **extra})


def idle_state(client, route, headers):
    deadline = time.monotonic() + 3
    while time.monotonic() < deadline:
        current = state(client, route, headers)
        if not current["pending"]:
            return current
        time.sleep(0.02)
    raise AssertionError("AI turn did not finish")


def test_two_clients_share_dialogue_but_only_speaker_knows_own_clue():
    calls = []

    async def model(context):
        calls.append(context)
        return "복도에 머물렀어요."

    client, rooms = make_clients(model)
    with client:
        route, host, guest = create_room(client)
        room = next(iter(rooms.rooms.values()))
        room.professor = "F"
        assert "revealed_professor" not in state(client, route, host)
        assert state(client, route, host)["is_host"]
        assert not state(client, route, guest)["is_host"]
        assert action(client, route, guest, "protect", target="A").status_code == 403
        assert action(client, route, host, "protect", target="A").status_code == 200
        surviving = state(client, route, host)["alive"]
        student = next(person for person in surviving if person != room.professor)
        assert action(client, route, guest, "question", target=student, text="어디에 있었나요?").status_code == 200
        first = idle_state(client, route, host)
        assert first["messages"] == state(client, route, guest)["messages"]
        assert [line["sequence"] for line in first["messages"]] == sorted(
            {line["sequence"] for line in first["messages"]}
        )
        assert first["messages"][-1]["source"] == "modal"
        assert calls[-1]["speaker"] == student
        assert calls[-1]["role"] == "학생"
        assert "교수 역할" not in json.dumps(calls[-1], ensure_ascii=False)
        assert set(calls[-1]) == {"speaker", "role", "personality", "memory", "public_dialogue", "question"}
        assert action(client, route, host, "question", target="F", text="어디에 있었나요?").status_code == 200
        idle_state(client, route, guest)
        assert calls[-1]["role"] == "교수"
        assert calls[-1]["memory"] != calls[0]["memory"]
        assert action(client, route, host, "guess", target="F").status_code == 200
        assert state(client, route, guest)["revealed_professor"] == "F"


def test_pending_room_is_not_charged_twice_and_other_users_cannot_read():
    async def model(_):
        await asyncio.sleep(0.2)
        return "기억을 다시 짚어 볼게요."

    client, _ = make_clients(model)
    with client:
        route, host, guest = create_room(client)
        assert client.get(route, headers=join_headers()).status_code == 403
        assert client.get(route, headers={"x-mafia-member": guest["x-mafia-member"]}).status_code == 401
        assert action(client, route, host, "protect", target="A").status_code == 200
        revision = state(client, route, host)["revision"]
        body = {"kind": "speak", "revision": revision}
        assert client.post(route + "/actions", headers=host, json=body).status_code == 200
        assert client.post(route + "/actions", headers=host, json=body).status_code == 409
        assert idle_state(client, route, guest)["messages"] == state(client, route, host)["messages"]
        assert sum(item["source"] == "modal" for item in state(client, route, host)["messages"]) == 1
        assert client.post(route + "/leave", headers=host).status_code == 200
        assert state(client, route, guest)["is_host"]


def test_missing_or_failed_provider_keeps_public_game_usable():
    async def failed(_):
        raise httpx.ReadTimeout("test")

    client, _ = make_clients(failed)
    with client:
        route, host, guest = create_room(client)
        assert action(client, route, host, "protect", target="A").status_code == 200
        assert action(client, route, host, "speak").status_code == 200
        result = idle_state(client, route, guest)
        assert result["messages"][-1]["source"] == "scripted"
        assert result["messages"] == state(client, route, host)["messages"]


def test_modal_disabled_and_monthly_limit_prevent_network_calls(monkeypatch):
    monkeypatch.setenv("MAFIA_MODAL_URL", "https://team--mafia.modal.run")
    monkeypatch.setenv("MAFIA_MODAL_KEY", "test-key")
    monkeypatch.setenv("MAFIA_MODAL_SECRET", "test-secret")
    monkeypatch.delenv("MAFIA_MODAL_BUDGET_VERIFIED", raising=False)
    monkeypatch.setenv("MAFIA_MODAL_MONTHLY_LIMIT", "1")
    sent = []

    async def simulate(request):
        sent.append(request)
        assert request.headers["modal-key"] == "test-key"
        assert request.headers["modal-secret"] == "test-secret"
        assert json.loads(request.content) == {"speaker": "B"}
        return httpx.Response(200, json={"utterance": "B의 대사입니다."})

    async def test():
        transport = httpx.MockTransport(simulate)
        assert await ModalDialogue.from_env(transport=transport)({"speaker": "B"}) is None
        assert not sent
        monkeypatch.setenv("MAFIA_MODAL_BUDGET_VERIFIED", "true")
        dialogue = ModalDialogue.from_env(transport=transport)
        assert await dialogue({"speaker": "B"}) == "B의 대사입니다."
        assert await dialogue({"speaker": "B"}) is None
        assert len(sent) == 1

    asyncio.run(test())


def test_provider_budget_error_suppresses_further_gpu_calls():
    attempts = []

    async def simulate(request):
        attempts.append(request)
        return httpx.Response(402)

    async def test():
        dialogue = ModalDialogue(
            "https://team--mafia.modal.run", "test-key", "test-secret", 10, 10,
            httpx.MockTransport(simulate),
        )
        assert await dialogue({"speaker": "A"}) is None
        assert await dialogue({"speaker": "B"}) is None
        assert len(attempts) == 1

    asyncio.run(test())
