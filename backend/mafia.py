"""Authoritative room state for the one-worker Render service."""
import asyncio
from collections import deque
from dataclasses import dataclass, field
from datetime import datetime, timezone
import hmac
import os
import secrets
import time
from typing import Literal
from urllib.parse import urlsplit

import httpx
from fastapi import APIRouter, HTTPException, Request, Response
from pydantic import BaseModel, Field, ValidationError

from mafia_dialogue import FOCUSES, grounded_line


@dataclass(frozen=True)
class Character:
    name: str
    trait: str
    style: str
    observation: str


CHARACTERS = {
    "A": Character("한서연", "신중한 기록 담당", "존댓말로 사실과 추측을 구분한다.",
                   "아홉 시에 자료실 불이 꺼지는 걸 복도 창가에서 봤어요. 문 안은 못 봤어요."),
    "B": Character("이도윤", "직설적인 체육부원", "짧고 단호한 반말을 쓴다.",
                   "복도에서 급한 발소리만 들었어. 누군지는 못 봤어."),
    "C": Character("윤하린", "시간을 따지는 반장", "장소나 순서를 정확히 정리해 말한다.",
                   "복도 시계가 아홉 시 오 분일 때 열람실 앞에 도착했어요. 그전 일은 몰라요."),
    "D": Character("고은재", "질문이 많은 토론부원", "확인한 사실을 먼저 말하고 짧게 되묻는다.",
                   "계단 아래에서 문 닫는 소리를 들었어요. 어느 문인지는 못 봤어요."),
    "E": Character("정민호", "물건을 잘 기억하는 도서부원", "물건의 위치나 모습에 주목하며 차분히 말한다.",
                   "불이 꺼지기 전에 책을 반납했어요. 반납대에 파란 노트가 있었어요."),
    "F": Character("서지우", "분위기를 풀어 주는 막내", "짧고 편한 입말로 말한다.",
                   "난 입구에 있었어. 책장 넘기는 소리는 났는데, 문 쪽은 잘 안 보였어."),
}
CASE_INTRO = "어젯밤 자료실 출입카드가 사라졌습니다. 여섯 사람 중 한 명은 학생으로 위장한 교수입니다."
DEMO_TESTIMONIES = {
    "C": "아홉 시 오 분에 열람실 앞에 도착했어요. 아래층 계단 방화문은 그때도 잠겨 있었어요.",
    "D": "아홉 시 오 분에 아래층 계단 방화문을 지나왔어요. 자료실 쪽으로는 안 갔어요.",
}
FOLLOW_UPS = {
    "A": ("그 시간을 어떻게 확인했나요?", "정확히 어느 쪽에 있었나요?", "직접 확인한 물건이 있나요?", "같은 일을 봤거나 들은 분 있나요?"),
    "B": ("몇 시였는지 딱 기억나?", "어느 쪽이었어?", "뭐 눈에 띈 물건 없어?", "그거 확인해 줄 사람 있어?"),
    "C": ("그게 불이 꺼지기 전인가요, 후인가요?", "복도와 열람실 중 어디였나요?", "그 물건은 언제부터 거기 있었나요?", "같은 시간에 다른 분은 어디에 있었나요?"),
    "D": ("그 시각을 같이 확인한 사람이 있나요?", "지나간 길을 짚어 볼 수 있나요?", "그 물건이 단서가 될 만한 근거가 있나요?", "서로 다르게 기억하는 부분이 있나요?"),
    "E": ("책을 반납하기 전에도 그랬나요?", "그게 반납대에 가까운 쪽이었나요?", "그 물건의 색이나 위치가 기억나나요?", "같은 물건을 보신 분이 있나요?"),
    "F": ("혹시 그게 몇 시쯤이었어?", "잠깐, 어디에서 있었던 일이야?", "그때 주변에 뭐가 놓여 있었어?", "다른 사람도 그렇게 기억해?"),
}
MAX_ROUNDS = 3
ROOM_IDLE_SECONDS = 45 * 60


class ModalDialogue:
    """A process-local quota plus an explicit opt-in to the provider's billing caps."""

    def __init__(self, url, key, secret, monthly_limit, daily_limit, transport=None):
        self.url, self.key, self.secret = url, key, secret
        self.monthly_limit, self.daily_limit = monthly_limit, daily_limit
        self.transport = transport
        self.period = ""
        self.day = ""
        self.monthly = self.daily = 0
        self.busy = False
        self.retry_after = 0

    @classmethod
    def from_env(cls, transport=None):
        url = os.getenv("MAFIA_MODAL_URL", "").strip()
        try:
            parsed = urlsplit(url)
        except ValueError:
            parsed = urlsplit("")
        verified = os.getenv("MAFIA_MODAL_BUDGET_VERIFIED") == "true"
        if (not verified or parsed.scheme != "https" or not parsed.hostname
                or not parsed.hostname.endswith(".modal.run") or parsed.username
                or parsed.password or parsed.query or parsed.fragment):
            url = ""

        def limit(name, default):
            try:
                return max(0, min(default, int(os.getenv(name, default))))
            except ValueError:
                return 0

        return cls(url, os.getenv("MAFIA_MODAL_KEY", "").strip(), os.getenv("MAFIA_MODAL_SECRET", "").strip(),
                   limit("MAFIA_MODAL_MONTHLY_LIMIT", 180), limit("MAFIA_MODAL_DAILY_LIMIT", 24), transport)

    async def __call__(self, context):
        if not (self.url and self.key and self.secret) or self.busy or time.monotonic() < self.retry_after:
            return None
        day = datetime.now(timezone.utc).date().isoformat()
        if day[:7] != self.period:
            self.period, self.monthly = day[:7], 0
        if day != self.day:
            self.day, self.daily = day, 0
        if self.daily >= self.daily_limit or self.monthly >= self.monthly_limit:
            return None
        self.daily += 1
        self.monthly += 1
        self.busy = True
        try:
            async with httpx.AsyncClient(transport=self.transport, timeout=35) as client:
                response = await client.post(self.url, json=context, headers={
                    "Modal-Key": self.key, "Modal-Secret": self.secret,
                })
                response.raise_for_status()
                return response.json()
        except (httpx.HTTPError, ValueError, AttributeError) as exc:
            billing_block = isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code in (402, 403, 429)
            self.retry_after = time.monotonic() + (900 if billing_block else 20)
            return None
        finally:
            self.busy = False


class Join(BaseModel):
    nickname: str = Field(min_length=1, max_length=20)


class Action(BaseModel):
    kind: Literal["protect", "speak", "question", "chat", "guess", "next_night"]
    revision: int = Field(ge=0)
    target: str = Field(default="", max_length=1)
    text: str = Field(default="", max_length=160)


@dataclass
class Member:
    nickname: str
    last_chat: float = -2


@dataclass
class Room:
    id: str
    professor: str
    members: dict[str, Member]
    host: str
    revision: int = 0
    phase: str = "night"
    round: int = 1
    alive: list[str] = field(default_factory=lambda: list(CHARACTERS))
    spoken: set[str] = field(default_factory=set)
    questions: int = 0
    messages: deque = field(default_factory=lambda: deque(maxlen=80))
    sequence: int = 0
    last_seen: float = field(default_factory=time.monotonic)
    attacked: str = ""
    pending: bool = False
    result: str = ""

    def add_message(self, speaker, text, source, elapsed_ms=None):
        self.sequence += 1
        line = {"sequence": self.sequence, "speaker": speaker, "text": text, "source": source}
        if elapsed_ms is not None:
            line["elapsed_ms"] = elapsed_ms
        self.messages.append(line)

    def public_state(self, member):
        snapshot = {
            "room_id": self.id, "revision": self.revision, "phase": self.phase, "round": self.round,
            "members": [item.nickname for item in self.members.values()], "is_host": member == self.host,
            "alive": self.alive[:], "pending": self.pending, "can_speak": bool(set(self.alive) - self.spoken),
            "messages": list(self.messages), "result": self.result,
        }
        if self.phase == "finished":
            snapshot["revealed_professor"] = self.professor
        return snapshot


class MafiaRooms:
    def __init__(self, dialogue=None, *, dialogue_source="modal", demo_professor=None):
        self.dialogue = dialogue or ModalDialogue.from_env()
        self.dialogue_source = dialogue_source
        if demo_professor is not None and demo_professor not in CHARACTERS:
            raise ValueError("Unknown demo professor")
        self.demo_professor = demo_professor
        self.rooms = {}
        self.tasks = set()

    def find(self, room_id):
        now = time.monotonic()
        for expired in [code for code, room in self.rooms.items() if now - room.last_seen > ROOM_IDLE_SECONDS]:
            del self.rooms[expired]
        return self.rooms.get(room_id)

    def create(self, nickname):
        self.find("")
        if len(self.rooms) >= 12:
            raise HTTPException(429, "열린 방이 많습니다. 잠시 후 다시 시도해 주세요.")
        room_id = secrets.token_hex(5).upper()
        while room_id in self.rooms:
            room_id = secrets.token_hex(5).upper()
        token = secrets.token_urlsafe(24)
        professor = self.demo_professor or secrets.choice(list(CHARACTERS))
        room = Room(room_id, professor, {token: Member(nickname)}, token)
        if self.demo_professor:
            # The browser benchmark starts at the first discussion with the
            # whole cast and a repeatable scenario for model comparisons.
            room.phase = "day"
            room.add_message("진행", CASE_INTRO, "system")
        self.rooms[room_id] = room
        return room, token

    def join(self, room, nickname):
        if len(room.members) >= 6:
            raise HTTPException(409, "방 정원이 찼습니다.")
        if any(member.nickname == nickname for member in room.members.values()):
            raise HTTPException(409, "같은 닉네임의 참가자가 있습니다.")
        token = secrets.token_urlsafe(24)
        room.members[token] = Member(nickname)
        room.last_seen = time.monotonic()
        return token

    def act(self, room, token, action):
        kind = action.kind
        if kind == "chat":
            member = room.members[token]
            now = time.monotonic()
            if now - member.last_chat < 1:
                raise HTTPException(429, "채팅은 잠시 후 다시 보내 주세요.")
            member.last_chat = now
            room.add_message(member.nickname, clean_text(action.text), "player")
            return
        if room.revision != action.revision or room.pending:
            raise HTTPException(409, "방 상태가 바뀌었습니다. 갱신된 화면에서 다시 시도해 주세요.")
        if kind != "question" and token != room.host:
            raise HTTPException(403, "진행자만 할 수 있는 행동입니다.")
        if kind == "protect" and room.phase == "night":
            self.protect(room, action.target)
        elif room.phase == "day":
            self.day_action(room, action)
        else:
            raise HTTPException(409, "지금 단계에서 할 수 없는 행동입니다.")
        room.revision += 1

    def protect(self, room, target):
        self.require_alive(room, target)
        attacked = secrets.choice([person for person in room.alive if person != room.professor])
        room.attacked = attacked
        if attacked != target:
            room.alive.remove(attacked)
            result = f"밤사이 학생 {attacked}이(가) 제외됐습니다."
        else:
            result = "밤사이 아무도 제외되지 않았습니다."
        room.add_message("진행", result, "system")
        room.phase = "day"

    def day_action(self, room, action):
        if action.kind == "speak":
            speaker = next((name for name in room.alive if name not in room.spoken), None)
            if not speaker:
                raise HTTPException(409, "오늘의 AI 발언이 모두 끝났습니다.")
            room.spoken.add(speaker)
            self.generate(room, speaker, "")
        elif action.kind == "question":
            self.require_alive(room, action.target)
            if room.questions >= 4:
                raise HTTPException(429, "이번 낮의 질문 횟수를 모두 사용했습니다.")
            question = clean_text(action.text)
            room.questions += 1
            room.add_message("질문 · " + action.target, question, "player")
            self.generate(room, action.target, question)
        elif action.kind == "guess":
            self.require_alive(room, action.target)
            room.result = "교수를 찾았습니다." if action.target == room.professor else "지목한 학생은 교수가 아니었습니다."
            room.phase = "finished"
        elif action.kind == "next_night":
            if room.round >= MAX_ROUNDS:
                raise HTTPException(409, "마지막 낮입니다. 교수를 지목해 주세요.")
            room.round += 1
            room.phase = "night"
            room.spoken.clear()
            room.questions = 0
        else:
            raise HTTPException(409, "지금 단계에서 할 수 없는 행동입니다.")

    @staticmethod
    def require_alive(room, target):
        if target not in room.alive:
            raise HTTPException(422, "현재 남아 있는 학생을 골라 주세요.")

    def generate(self, room, speaker, question):
        character = CHARACTERS[speaker]
        is_professor = speaker == room.professor
        testimony = DEMO_TESTIMONIES.get(speaker, character.observation) if self.demo_professor else character.observation
        memory = testimony
        if is_professor:
            memory = "공개용 알리바이 " + memory + " 비밀 행동은 자료실 출입카드를 가져갔다."
            if room.attacked:
                memory += f" 이번 밤에 {room.attacked}을(를) 공격했다."
        context = {
            "speaker": speaker, "role": "교수" if is_professor else "학생",
            "personality": f"{character.name}. {character.trait}. {character.style}",
            "memory": memory,
            "public_dialogue": [
                {"speaker": msg["speaker"], "text": msg["text"]} for msg in room.messages
                if msg["source"] in (self.dialogue_source, self.dialogue_source + "_choice", "scripted")
                or (msg["source"] == "system" and msg["text"].startswith("밤사이"))
            ][-3:],
            "question": question,
        }
        room.pending = True
        task = asyncio.create_task(self.finish_turn(room, context, testimony))
        self.tasks.add(task)
        task.add_done_callback(self.tasks.discard)

    async def finish_turn(self, room, context, fallback):
        started = time.monotonic()
        try:
            reply = await asyncio.wait_for(self.dialogue(context), timeout=40)
        except (TimeoutError, httpx.HTTPError, ValueError, AttributeError, KeyError, TypeError):
            reply = None
        finally:
            room.pending = False
            room.revision += 1
        elapsed = round((time.monotonic() - started) * 1000)
        speaker = context["speaker"]
        speech = reply.get("utterance") if isinstance(reply, dict) else reply
        focus = reply.get("focus") if isinstance(reply, dict) else None
        line = grounded_line(fallback, speech)
        if line:
            room.add_message(speaker, line, self.dialogue_source, elapsed)
        elif focus in FOCUSES:
            line = fallback + " " + FOLLOW_UPS[speaker][FOCUSES.index(focus)]
            room.add_message(speaker, line, self.dialogue_source + "_choice", elapsed)
        else:
            room.add_message(speaker, fallback, "scripted", elapsed)


def clean_text(value):
    value = " ".join(value.split())
    if not value or any(ord(ch) < 32 for ch in value):
        raise HTTPException(422, "내용을 입력해 주세요.")
    return value


def mafia_router(access_token, rooms):
    router = APIRouter(prefix="/v1/mafia/rooms")

    def authorize(request):
        if not access_token:
            raise HTTPException(503, "마피아룸 접속 설정을 확인해 주세요.")
        if not hmac.compare_digest(request.headers.get("x-quiz-token", "").encode(), access_token.encode()):
            raise HTTPException(401, "팀 참가 코드를 확인해 주세요.")

    def get_room(room_id):
        room = rooms.find(room_id.upper())
        if not room:
            raise HTTPException(404, "방이 종료되었거나 방 코드가 맞지 않습니다.")
        return room

    def get_member(request, room):
        token = request.headers.get("x-mafia-member", "")
        if token not in room.members:
            raise HTTPException(403, "이 방에 먼저 참가해 주세요.")
        room.last_seen = time.monotonic()
        return token

    async def read(request, model):
        body = bytearray()
        try:
            async with asyncio.timeout(5):
                async for chunk in request.stream():
                    if len(body) + len(chunk) > 4096:
                        raise HTTPException(413, "요청 크기가 너무 큽니다.")
                    body.extend(chunk)
            return model.model_validate_json(body)
        except ValidationError as exc:
            raise HTTPException(422, "요청 내용을 확인해 주세요.") from exc
        except TimeoutError as exc:
            raise HTTPException(408, "요청 시간이 초과되었습니다.") from exc

    @router.post("")
    async def create(request: Request, response: Response):
        authorize(request)
        info = await read(request, Join)
        room, token = rooms.create(clean_text(info.nickname))
        response.headers["Cache-Control"] = "no-store"
        return {"room_id": room.id, "member_token": token}

    @router.post("/{room_id}/join")
    async def join(room_id: str, request: Request, response: Response):
        authorize(request)
        info = await read(request, Join)
        room = get_room(room_id)
        response.headers["Cache-Control"] = "no-store"
        return {"room_id": room.id, "member_token": rooms.join(room, clean_text(info.nickname))}

    @router.get("/{room_id}")
    async def state(room_id: str, request: Request, response: Response):
        authorize(request)
        room = get_room(room_id)
        response.headers["Cache-Control"] = "no-store"
        return room.public_state(get_member(request, room))

    @router.post("/{room_id}/actions")
    async def act(room_id: str, request: Request):
        authorize(request)
        action = await read(request, Action)
        room = get_room(room_id)
        member = get_member(request, room)
        rooms.act(room, member, action)
        return room.public_state(member)

    @router.post("/{room_id}/leave")
    async def leave(room_id: str, request: Request):
        authorize(request)
        room = get_room(room_id)
        token = get_member(request, room)
        del room.members[token]
        if not room.members:
            del rooms.rooms[room.id]
        elif token == room.host:
            room.host = next(iter(room.members))
            room.revision += 1
        return {"left": True}

    return router
