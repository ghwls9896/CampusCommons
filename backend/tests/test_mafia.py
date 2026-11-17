import asyncio
import json
import time

import httpx
from fastapi.testclient import TestClient

from app import Settings, create_app
from mafia import MafiaRooms, ModalDialogue
from mafia_demo import create_demo
from mafia_dialogue import OllamaDialogue, grounded_line, ollama_payload


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
        return "다들 어디에 있었나요?"

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
        return "누가 시간을 확인했나요?"

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
        assert await dialogue({"speaker": "B"}) == {"utterance": "B의 대사입니다."}
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


def test_browser_demo_generates_six_distinct_turns_with_isolated_role_context():
    turns = []

    async def ollama(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": [{"name": "replaceable-model"}]})
        payload = json.loads(request.content)
        assert payload["model"] == "replaceable-model"
        assert payload["format"]["required"] == ["focus", "utterance"]
        context = json.loads(payload["messages"][1]["content"])
        turns.append(context)
        follow_up = "제가 직접 봤는데, 누가 믿어줄래요?" if context["speaker"] == "B" else "누가 시간을 확인했나요?"
        return httpx.Response(200, json={"message": {"content": json.dumps({"focus": "time", "utterance": follow_up})}})

    model = OllamaDialogue("replaceable-model", transport=httpx.MockTransport(ollama))
    with TestClient(create_demo(model), base_url="http://localhost") as client:
        page = client.get("/")
        assert page.status_code == 200 and "교수를 찾아라" in page.text
        config = client.get("/demo/config").json()
        assert config["ready"] and config["model"] == "replaceable-model"
        assert len({person["name"] for person in config["characters"]}) == 6
        assert all(set(person) == {"id", "name", "trait"} for person in config["characters"])
        client.headers["x-quiz-token"] = config["token"]
        created = client.post("/v1/mafia/rooms", json={"nickname": "감사관"}).json()
        route = "/v1/mafia/rooms/" + created["room_id"]
        client.headers["x-mafia-member"] = created["member_token"]
        opening = state(client, route, {})
        assert opening["phase"] == "day" and len(opening["alive"]) == 6
        assert "revealed_professor" not in opening
        for _ in range(6):
            assert action(client, route, {}, "speak").status_code == 200
            final = idle_state(client, route, {})
        assert [turn["speaker"] for turn in turns] == list("ABCDEF")
        assert [turn["role"] for turn in turns].count("교수") == 1
        assert [turn["role"] for turn in turns].count("학생") == 5
        assert len({turn["memory"] for turn in turns}) == 6
        assert all("출입카드를 가져갔다" not in t["memory"] for t in turns if t["role"] == "학생")
        assert len(turns[0]["public_dialogue"]) < len(turns[4]["public_dialogue"])
        live = [item for item in final["messages"] if item["source"] in ("ollama", "ollama_choice")]
        assert len(live) == 6 and not final["can_speak"]
        assert all(isinstance(item["elapsed_ms"], int) for item in live)
        assert len({item["text"] for item in live}) == 6
        assert live[1]["source"] == "ollama_choice"
        assert live[1]["text"].endswith("몇 시였는지 딱 기억나?")


def test_generated_follow_up_cannot_replace_testimony_or_disclose_role():
    clue = "복도에서 급한 발소리만 들었어. 얼굴은 못 봤어."
    assert grounded_line(clue, "복도에 누가 있었어?") == clue + " 복도에 누가 있었어?"
    assert grounded_line(clue, "E가 가져가는 걸 직접 봤어.") is None
    assert grounded_line(clue, "제가 직접 봤는데, 누가 믿어줄래요?") is None
    assert grounded_line(clue, "내가 가져갔는데 누가 봤어?") is None


def test_model_swapping_changes_only_model_id_in_the_shared_prompt():
    context = {"speaker": "B", "role": "학생", "personality": "짧고 직설적", "memory": "발소리만 들었다",
               "public_dialogue": [], "question": "교수를 봤나?"}
    qwen = ollama_payload("qwen3.5:2b-q4_K_M", context)
    gemma = ollama_payload("gemma4:e2b-it-q4_K_M", context)
    assert qwen["model"] != gemma["model"]
    assert qwen["messages"] == gemma["messages"]
    assert qwen["format"] == gemma["format"]
