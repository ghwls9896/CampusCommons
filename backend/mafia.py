"""Authoritative room state for the one-worker Render service."""
import asyncio
from collections import deque
from dataclasses import dataclass, field
from datetime import datetime, timezone
import hmac
import os
import re
import secrets
import time
from typing import Literal
from urllib.parse import urlsplit

import httpx
from fastapi import APIRouter, HTTPException, Request, Response
from pydantic import BaseModel, Field, ValidationError


CHARACTERS = {
    "A": ("신중하고 존댓말을 쓴다", "나는 밤에 도서관 창가에서 책을 읽었다."),
    "B": ("직설적이고 짧게 말한다", "나는 밤에 복도를 걸었고 빈 의자가 놓인 것을 봤다."),
    "C": ("시간 순서대로 침착하게 설명한다", "나는 시계 종소리 뒤에 열람실에 도착했다."),
    "D": ("조심스럽게 반문한다", "나는 계단 쪽에서 발소리를 들었지만 얼굴은 보지 못했다."),
    "E": ("차분하게 세부를 확인하는 말투", "나는 불이 꺼지기 전에 책을 반납했다."),
    "F": ("편한 말투를 쓰고 다른 의견도 확인한다", "나는 입구에서 멈춰 있었고 책 넘기는 소리를 들었다."),
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
                return response.json().get("utterance")
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

    def add_message(self, speaker, text, source):
        self.sequence += 1
        self.messages.append({"sequence": self.sequence, "speaker": speaker, "text": text, "source": source})

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
    def __init__(self, dialogue=None):
        self.dialogue = dialogue or ModalDialogue.from_env()
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
        room = Room(room_id, secrets.choice(list(CHARACTERS)), {token: Member(nickname)}, token)
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
        personality, memory = CHARACTERS[speaker]
        is_professor = speaker == room.professor
        context = {
            "speaker": speaker, "role": "교수" if is_professor else "학생",
            "personality": personality,
            "memory": memory + (f" 실제로는 이번 밤에 {room.attacked}을(를) 공격했다. 공개 발언에서 비밀로 한다." if is_professor else ""),
            "public_dialogue": [
                {"speaker": msg["speaker"], "text": msg["text"]} for msg in room.messages
                if msg["source"] != "player"
            ][-6:],
            "question": question,
        }
        room.pending = True
        task = asyncio.create_task(self.finish_turn(room, context, memory))
        self.tasks.add(task)
        task.add_done_callback(self.tasks.discard)

    async def finish_turn(self, room, context, fallback):
        try:
            utterance = await asyncio.wait_for(self.dialogue(context), timeout=40)
        except (TimeoutError, httpx.HTTPError, ValueError, AttributeError):
            utterance = None
        finally:
            room.pending = False
            room.revision += 1
        if (isinstance(utterance, str) and 0 < len(utterance.strip()) <= 180
                and not re.search(r"(?:저는|제가|나는|내가|난)\s*교수(?:입니다|예요|에요|다|였)", utterance)):
            room.add_message(context["speaker"], " ".join(utterance.split()), "modal")
        else:
            room.add_message(context["speaker"], fallback, "scripted")


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
