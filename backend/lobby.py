"""One public library lobby per FastAPI app, served by a single Uvicorn worker."""
import asyncio
import base64
from dataclasses import dataclass, field
import json
import os
import time
from typing import Annotated, Literal
from urllib.parse import urlsplit
from uuid import UUID, uuid4

from fastapi import APIRouter, WebSocket, WebSocketDisconnect
import httpx
from pydantic import BaseModel, ConfigDict, Field, TypeAdapter, ValidationError


CAPACITY = 8
TICK_SECONDS = .1
MAX_MOVE_BYTES = 1024


class LobbyClose(Exception):
    def __init__(self, code: int, reason: str):
        super().__init__(reason)
        self.code = code
        self.reason = reason


@dataclass(frozen=True)
class Account:
    user_id: UUID
    nickname: str
    expires_at: int
    level: int


async def read_account(header: str, transport: httpx.AsyncBaseTransport | None) -> Account:
    scheme, _, token = header.partition(' ')
    if (scheme.lower() != 'bearer' or not token or len(token) > 8192
            or not token.isascii() or not token.isprintable() or ' ' in token):
        raise LobbyClose(4401, 'unauthorized')
    url = os.getenv('SUPABASE_URL', '').strip().rstrip('/')
    key = os.getenv('SUPABASE_PUBLISHABLE_KEY', '').strip()
    try:
        parsed = urlsplit(url)
        if (parsed.scheme != 'https' or not parsed.hostname or parsed.username is not None
                or parsed.path or parsed.query or parsed.fragment or not key.startswith('sb_publishable_')):
            raise ValueError
        async with httpx.AsyncClient(transport=transport, timeout=5) as client:
            response = await client.post(url + '/functions/v1/account', json={'action': 'read'},
                                         headers={'apikey': key, 'Authorization': 'Bearer ' + token})
        if response.status_code == 401:
            raise LobbyClose(4401, 'unauthorized')
        response.raise_for_status()
        profile = response.json()
        user_id = UUID(profile['user_id'])
        nickname = profile['nickname']
        if nickname == '':
            raise LobbyClose(4403, 'profile_not_ready')
        if not isinstance(nickname, str) or not 2 <= len(nickname) <= 12:
            raise ValueError
        level = profile['level']
        if type(level) is not int or not 1 <= level <= 15:
            raise ValueError
    except (httpx.HTTPError, httpx.InvalidURL, ValueError, KeyError, TypeError, AttributeError) as exc:
        raise LobbyClose(1013, 'account_unavailable') from exc

    # The account function verified this token with Supabase Auth. Use its expiry
    # only to bound the socket lifetime; identity always comes from that function.
    try:
        _, payload, _ = token.split('.')
        claims = json.loads(base64.urlsafe_b64decode(payload + '=' * (-len(payload) % 4)))
        expires = claims['exp']
        if type(expires) is not int or time.time() >= expires:
            raise ValueError
    except (ValueError, TypeError, KeyError) as exc:
        raise LobbyClose(4401, 'unauthorized') from exc
    return Account(user_id=user_id, nickname=nickname, expires_at=expires, level=level)


Coordinate = Annotated[float, Field(ge=-10000, le=10000, allow_inf_nan=False)]


class Move(BaseModel):
    model_config = ConfigDict(strict=True, extra='forbid')
    type: Literal['move']
    seq: int = Field(ge=0, le=2_147_483_647)
    x: Coordinate
    y: Coordinate
    z: Coordinate
    yaw: float = Field(ge=0, lt=360)
    motion: Literal['idle', 'walk', 'run']


AppearanceIndex = Annotated[int, Field(ge=0, le=255)]
ClothingName = Annotated[str, Field(max_length=64, pattern=r'^[A-Za-z0-9 _-]*$')]


class Appearance(BaseModel):
    model_config = ConfigDict(strict=True, extra='forbid')
    type: Literal['appearance']
    body: int = Field(ge=0, le=1)
    skin: AppearanceIndex
    hair: AppearanceIndex
    hair_color: AppearanceIndex
    items: list[ClothingName] = Field(min_length=5, max_length=5)
    colors: list[AppearanceIndex] = Field(min_length=5, max_length=5)


LOBBY_INPUT = TypeAdapter(Annotated[Move | Appearance, Field(discriminator='type')])


@dataclass(eq=False)
class Member:
    account: Account
    slot: int
    peer_id: str = field(default_factory=lambda: uuid4().hex)
    pose: Move | None = None
    appearance: Appearance | None = None

    def public_state(self) -> dict:
        state = {'id': self.peer_id, 'nickname': self.account.nickname, 'level': self.account.level, 'slot': self.slot,
                 'pose': self.pose.model_dump(exclude={'type'}) if self.pose else None}
        if self.appearance:
            state['appearance'] = self.appearance.model_dump(exclude={'type'})
        return state


class Lobby:
    def __init__(self):
        self.members: dict[UUID, Member] = {}
        self.revision = 0

    def join(self, account: Account) -> Member:
        prior = self.members.get(account.user_id)
        if prior:
            slot = prior.slot
        else:
            if len(self.members) == CAPACITY:
                raise LobbyClose(4409, 'lobby_full')
            occupied = {peer.slot for peer in self.members.values()}
            slot = next(i for i in range(CAPACITY) if i not in occupied)
        member = Member(account, slot)
        self.members[account.user_id] = member
        self.revision += 1
        return member

    def check_session(self, member: Member):
        if self.members.get(member.account.user_id) is not member:
            raise LobbyClose(4400, 'replaced')
        if time.time() >= member.account.expires_at:
            raise LobbyClose(4401, 'token_expired')

    def leave(self, member: Member):
        if self.members.get(member.account.user_id) is member:
            del self.members[member.account.user_id]
            self.revision += 1

    def move(self, member: Member, move: Move):
        if member.pose is None or move.seq > member.pose.seq:
            member.pose = move
            self.revision += 1

    def change_appearance(self, member: Member, appearance: Appearance):
        if appearance != member.appearance:
            member.appearance = appearance
            self.revision += 1

    def roster(self):
        return [peer.public_state() for peer in sorted(self.members.values(), key=lambda p: p.slot)]


async def receive_moves(socket: WebSocket, room: Lobby, member: Member):
    window_start = time.monotonic()
    count = 0
    while True:
        message = await socket.receive()
        if message['type'] == 'websocket.disconnect':
            return
        room.check_session(member)
        raw = message.get('text')
        if raw is None:
            raise LobbyClose(1003, 'text_required')
        if len(raw) > MAX_MOVE_BYTES or len(raw.encode('utf-8')) > MAX_MOVE_BYTES:
            raise LobbyClose(1009, 'message_too_large')
        now = time.monotonic()
        if now - window_start >= 1:
            window_start, count = now, 0
        count += 1
        if count > 30:
            raise LobbyClose(1008, 'rate_limited')
        try:
            payload = LOBBY_INPUT.validate_json(raw)
        except ValidationError as exc:
            raise LobbyClose(1008, 'invalid_move') from exc
        if isinstance(payload, Move):
            room.move(member, payload)
        else:
            room.change_appearance(member, payload)


async def publish_roster(socket: WebSocket, room: Lobby, member: Member):
    last_revision = None
    while True:
        room.check_session(member)
        if room.revision != last_revision:
            frame = {'type': 'snapshot', 'revision': room.revision, 'players': room.roster()}
            if last_revision is None:
                frame.update(type='welcome', self_id=member.peer_id, appearance_version=1)
            last_revision = room.revision
            try:
                async with asyncio.timeout(3):
                    await socket.send_json(frame)
            except TimeoutError as exc:
                raise LobbyClose(1013, 'slow_connection') from exc
        await asyncio.sleep(TICK_SECONDS)


async def serve_member(socket: WebSocket, room: Lobby, member: Member):
    tasks = [asyncio.create_task(publish_roster(socket, room, member)),
             asyncio.create_task(receive_moves(socket, room, member))]
    try:
        done, _ = await asyncio.wait(tasks, return_when=asyncio.FIRST_COMPLETED)
        for task in done:
            task.result()
    finally:
        for task in tasks:
            task.cancel()
        await asyncio.gather(*tasks, return_exceptions=True)


def lobby_router(account_transport: httpx.AsyncBaseTransport | None = None) -> APIRouter:
    router = APIRouter()
    room = Lobby()
    pending_auth = 0

    @router.websocket('/v1/lobby/ws')
    async def lobby_socket(socket: WebSocket):
        nonlocal pending_auth
        await socket.accept()
        try:
            if pending_auth >= 2 * CAPACITY:
                raise LobbyClose(1013, 'account_unavailable')
            pending_auth += 1
            try:
                account = await read_account(socket.headers.get('authorization', ''), account_transport)
            finally:
                pending_auth -= 1
            member = room.join(account)
            try:
                await serve_member(socket, room, member)
            finally:
                room.leave(member)
        except LobbyClose as error:
            await socket.close(code=error.code, reason=error.reason)
        except WebSocketDisconnect:
            pass

    return router
