"""One two-seat quiz table per Uvicorn worker. Documents and tickets only live in RAM."""
import asyncio
from dataclasses import dataclass, field
import math
import secrets
import time

import httpx
from fastapi import APIRouter, Request, Response
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ConfigDict, Field, ValidationError

from lobby import Account, LobbyClose, read_account
from quiz import AnswerSubmission, QuizError, select_pdf_range, validate_grades

LIMITS = (10, 15, 20)
LEASE = 45  # Clients poll during PDF upload, AI processing and play.


class Answer(BaseModel):
    model_config = ConfigDict(strict=True, extra='forbid')
    round_id: str = Field(min_length=1, max_length=64)
    level: int = Field(ge=1, le=3)
    choice: int = Field(default=-1, ge=-1, le=3)
    text: str = Field(default='', max_length=1000)


class Draft(Answer):
    seq: int | None = Field(default=None, ge=0, le=2_147_483_647)


@dataclass(eq=False)
class Seat:
    slot: int
    account: Account
    ticket: str = field(default_factory=lambda: secrets.token_urlsafe(32))
    expires: float = 0
    alarm: asyncio.TimerHandle | None = None


@dataclass
class Sheet:
    pdf: bytes | None = None
    applied_level: int | None = None
    pages: int = 0
    first_page: int = 1
    uploading: bool = False
    questions: list = field(default_factory=list)
    answers: list = field(default_factory=list)
    times: list = field(default_factory=list)
    points: list = field(default_factory=list)
    feedback: list = field(default_factory=list)
    draft_choice: int = -1
    draft_text: str = ''
    draft_seq: int = -1
    started: float = 0

    @property
    def ready(self):
        return bool(self.pdf or self.questions)

    def record(self, choice, text, elapsed):
        index = len(self.answers)
        self.answers.append(choice if index == 0 else text)
        self.times.append(min(LIMITS[index] * 1000, max(0, round(elapsed * 1000))))
        self.started += elapsed
        self.draft_choice, self.draft_text, self.draft_seq = -1, '', -1

    def expire(self, now):
        while len(self.answers) < len(LIMITS):
            limit = LIMITS[len(self.answers)]
            if now < self.started + limit:
                return
            self.record(self.draft_choice, self.draft_text, limit)


class Round:
    def __init__(self):
        self.id = secrets.token_hex(16)
        self.sheets = [Sheet(), Sheet()]
        self.phase = 'waiting'
        self.message = ''
        self.failed_at = ''
        self.results = []
        self.target_level = 0
        self.job = None

    def check_id(self, expected):
        if expected != self.id:
            raise QuizError('stale_round', '참가자가 바뀌었습니다. 현재 대결을 확인해 주세요.', 409)


class Duel:
    def __init__(self, settings, generator, grader, use_budget, worker, hosted_quiz=None):
        self.settings = settings
        self.hosted_quiz = hosted_quiz
        self.generator = generator
        self.grader = grader
        self.use_budget = use_budget
        self.worker = worker
        self.pending_uploads = 0
        self.seats = {}
        self.round = Round()

    def touch(self, seat):
        if seat.alarm:
            seat.alarm.cancel()
        ttl = min(LEASE, max(0, seat.account.expires_at - time.time()))
        seat.expires = time.monotonic() + ttl
        seat.alarm = asyncio.get_running_loop().call_later(ttl, self.leave, seat)

    def sweep(self):
        now = time.monotonic()
        for seat in list(self.seats.values()):
            if now >= seat.expires or time.time() >= seat.account.expires_at:
                self.leave(seat)

    def leave(self, seat):
        if self.seats.get(seat.slot) is not seat:
            return
        if seat.alarm:
            seat.alarm.cancel()
        del self.seats[seat.slot]
        if self.round.phase == 'results' and self.seats:
            # The second reader keeps the final comparison even if the winner exits first.
            return
        if self.round.job:
            self.round.job.cancel()
        self.round = Round()

    def join(self, slot, account):
        self.sweep()
        occupant = self.seats.get(slot)
        if occupant and occupant.account.user_id == account.user_id:
            # Resume a lost join response or a reopened window; invalidate the old capability.
            occupant.ticket = secrets.token_urlsafe(32)
            occupant.account = account
            self.touch(occupant)
            return occupant
        if occupant or self.round.phase == 'results':
            raise QuizError('seat_taken', '이미 사용 중인 자리입니다.', 409)
        if any(s.account.user_id == account.user_id for s in self.seats.values()):
            raise QuizError('already_seated', '이 계정은 이미 다른 자리에 앉아 있습니다.', 409)
        seat = Seat(slot, account)
        self.seats[slot] = seat
        self.touch(seat)
        if len(self.seats) == 2:
            self.round.phase = 'upload'
            self.round.target_level = (sum(s.account.level for s in self.seats.values()) + 1) // 2
        return seat

    def identify(self, request):
        scheme, _, token = request.headers.get('authorization', '').partition(' ')
        seat = self.seat_for_ticket(token if scheme.lower() == 'bearer' else '')
        self.touch(seat)
        return seat

    def seat_for_ticket(self, ticket):
        self.sweep()
        if ticket.isascii() and 0 < len(ticket) <= 128:
            for seat in self.seats.values():
                if secrets.compare_digest(seat.ticket, ticket):
                    return seat
        raise QuizError('seat_expired', '좌석 연결이 종료되었습니다. 다시 앉아 주세요.', 401)

    def renew(self, ticket, account):
        seat = self.seat_for_ticket(ticket)
        if seat.account.user_id != account.user_id:
            raise QuizError('seat_owner_mismatch', '이 계정으로 연결한 좌석이 아닙니다.', 403)
        if account.expires_at >= seat.account.expires_at:
            seat.account = account
        self.touch(seat)
        return seat

    def update(self):
        rd = self.round
        if rd.phase == 'playing':
            now = time.monotonic()
            for sheet in rd.sheets:
                sheet.expire(now)
            if all(len(sheet.answers) == 3 for sheet in rd.sheets):
                self.start_job('grading')

    def state(self, seat):
        self.update()
        rd = self.round
        me = rd.sheets[seat.slot]
        players = [
            {'slot': s.slot, 'nickname': s.account.nickname, 'level': s.account.level,
             'ready': rd.sheets[s.slot].ready, 'finished': len(rd.sheets[s.slot].answers) == 3}
            for s in sorted(self.seats.values(), key=lambda x: x.slot)
        ]
        phase, question, remain = rd.phase, None, 0
        if phase == 'playing':
            index = len(me.answers)
            if index == len(LIMITS):
                phase = 'grading'
            else:
                question = {key: me.questions[index][key] for key in ('level', 'prompt', 'options', 'answer_type')}
                remain = max(0, math.ceil((me.started + LIMITS[index] - time.monotonic()) * 1000))
        review = []
        if phase == 'results':
            for i, q in enumerate(me.questions):
                answer = me.answers[i]
                if i == 0:
                    answer = q['options'][answer] if answer >= 0 else ''
                review.append({'level': i + 1, 'prompt': q['prompt'], 'answer': answer,
                               'model_answer': q['model_answer'], 'explanation': q['explanation'],
                               'source_page': q['source_page'], 'score': me.points[i], 'feedback': me.feedback[i]})
        difficulty_mode = 'account'
        if self.hosted_quiz:
            if not all(sheet.questions for sheet in rd.sheets):
                difficulty_mode = 'pending'
            elif any(sheet.applied_level is None for sheet in rd.sheets):
                difficulty_mode = 'standard'
        return {'difficulty_mode': difficulty_mode, 'phase': phase, 'round_id': rd.id, 'self_slot': seat.slot, 'players': players, 'review': review,
                'question': question, 'remaining_ms': remain, 'results': rd.results,
                'message': rd.message, 'target_level': rd.target_level,
                'can_upload': rd.phase == 'upload' or (rd.phase == 'error' and rd.failed_at == 'generating')}

    def accept_upload(self, seat, expected):
        rd = self.round
        rd.check_id(expected)
        if rd.phase != 'upload' and not (rd.phase == 'error' and rd.failed_at == 'generating'):
            raise QuizError('wrong_phase', '두 사람이 앉고 PDF 제출 단계가 되면 선택해 주세요.', 409)
        sheet = rd.sheets[seat.slot]
        if sheet.uploading:
            raise QuizError('upload_busy', '이미 이 자리의 PDF를 읽고 있습니다.', 409)
        return sheet

    def start_job(self, phase):
        rd = self.round
        rd.phase = phase
        rd.message = rd.failed_at = ''
        rd.job = asyncio.create_task(self.run_job(rd, phase))

    async def run_job(self, rd, phase):
        try:
            if not self.hosted_quiz and not self.settings.api_key:
                raise QuizError('server_configuration', '퀴즈 서버의 AI 설정이 필요합니다.', 503)
            async with self.worker, httpx.AsyncClient() as client:
                for sheet in rd.sheets:
                    if phase == 'generating' and not sheet.questions:
                        self.use_budget()
                        timeout = self.settings.generation_timeout + (90 if self.hosted_quiz else 0)
                        async with asyncio.timeout(timeout):
                            if self.hosted_quiz:
                                # If an older deployment produced the other set, keep both on
                                # the same three standard stages, even during a Render rollout.
                                other = next((s for s in rd.sheets if s.questions), None)
                                target = other.applied_level if other else rd.target_level
                                quiz = await self.hosted_quiz.generate(sheet.pdf, sheet.pages, client, target)
                                sheet.applied_level = quiz.get('target_level')
                                if other and sheet.applied_level != other.applied_level:
                                    raise QuizError('quiz_service_changed', '출제 중 서버 버전이 바뀌었습니다. 두 사람이 다시 착석해 주세요.', 503)
                            else:
                                quiz = await self.generator(sheet.pdf, sheet.pages, self.settings.api_key,
                                                            self.settings.model, client, rd.target_level)
                        sheet.questions = [dict(q, source_page=q['source_page'] + sheet.first_page - 1)
                                           for q in quiz['questions']]
                        sheet.pdf = None
                    elif phase == 'grading' and not sheet.points:
                        timeout = self.settings.grading_timeout + (90 if self.hosted_quiz else 0)
                        async with asyncio.timeout(timeout):
                            sheet.points, sheet.feedback = await self.score(sheet, client)
            if self.round is not rd:
                return
            if phase == 'generating':
                start = time.monotonic()
                for sheet in rd.sheets:
                    sheet.started = start
                rd.phase = 'playing'
            else:
                rd.results = [self.summary(self.seats[i], rd.sheets[i]) for i in range(2)]
                rd.phase = 'results'
        except asyncio.CancelledError:
            raise
        except Exception as exc:
            rd.phase = 'error'
            rd.failed_at = phase
            rd.message = exc.message if isinstance(exc, QuizError) else 'AI 응답을 완료하지 못했습니다. 다시 시도해 주세요.'
        finally:
            rd.job = None

    async def score(self, sheet, client):
        choice = 10 if sheet.answers[0] == sheet.questions[0]['correct_index'] else 0
        first_feedback = '정답입니다.' if choice else ('제출된 답이 없습니다.' if sheet.answers[0] < 0 else '선택한 보기가 정답과 다릅니다.')
        if not any(sheet.answers[1:]):
            return [choice, 0, 0], [first_feedback, '제출된 답이 없습니다.', '제출된 답이 없습니다.']
        answers = [AnswerSubmission(level=i + 1, prompt=sheet.questions[i]['prompt'],
                                    model_answer=sheet.questions[i]['model_answer'],
                                    grading_criteria=sheet.questions[i]['grading_criteria'],
                                    answer=sheet.answers[i]) for i in (1, 2)]
        self.use_budget()
        if self.hosted_quiz:
            result = await self.hosted_quiz.grade(answers, client)
        else:
            result = await self.grader(answers, self.settings.api_key, self.settings.model, client)
        grades = validate_grades(result, answers)['grades']
        return [choice] + [g['score'] for g in grades], [first_feedback] + [g['feedback'] for g in grades]

    @staticmethod
    def summary(seat, sheet):
        correct = sheet.points.count(10)
        return {'slot': seat.slot, 'nickname': seat.account.nickname, 'score': sum(sheet.points),
                'correct': correct, 'accuracy': round(correct * 100 / 3, 1), 'elapsed_ms': sum(sheet.times),
                'stages': [{'level': i + 1, 'score': score, 'elapsed_ms': sheet.times[i]}
                           for i, score in enumerate(sheet.points)]}

    def submit(self, seat, answer):
        rd = self.round
        rd.check_id(answer.round_id)
        self.update()
        sheet = rd.sheets[seat.slot]
        if len(sheet.answers) >= answer.level:
            return  # Late timeout or repeated POST: never mutate the next question.
        if rd.phase != 'playing' or answer.level != len(sheet.answers) + 1:
            raise QuizError('wrong_level', '현재 단계의 답을 제출해 주세요.', 409)
        draft = isinstance(answer, Draft)
        if answer.level == 1:
            if (answer.choice < 0 and not draft) or answer.text:
                raise QuizError('invalid_answer', '보기 하나를 선택해 주세요.')
        elif answer.choice != -1 or (not draft and not answer.text.strip()) or len(answer.text) > (300 if answer.level == 2 else 1000):
            raise QuizError('invalid_answer', '제한 길이 안에서 답안을 입력해 주세요.')
        now = time.monotonic()
        if now >= sheet.started + LIMITS[answer.level - 1]:
            sheet.expire(now)
        elif draft:
            # Optional sequence lets polling clients discard delayed writes to the same stage.
            if sheet.draft_seq == -1 or (answer.seq is not None and answer.seq > sheet.draft_seq):
                sheet.draft_choice = answer.choice
                sheet.draft_text = answer.text.strip()
                sheet.draft_seq = answer.seq if answer.seq is not None else -1
        else:
            sheet.record(answer.choice, answer.text.strip(), now - sheet.started)


def duel_router(settings, account_transport, generator, grader, use_budget, read_body, worker, hosted_quiz=None):
    table = Duel(settings, generator, grader, use_budget, worker, hosted_quiz)
    router = APIRouter(prefix='/v1/duel')
    no_store = {'Cache-Control': 'no-store'}

    async def verified_account(request):
        try:
            return await read_account(request.headers.get('authorization', ''), account_transport)
        except LobbyClose as exc:
            status = {4401: 401, 4403: 403}.get(exc.code, 503)
            raise QuizError(exc.reason, '로그인 상태와 계정 서버 연결을 확인해 주세요.', status) from exc

    def admission(seat):
        return JSONResponse({'ticket': seat.ticket, 'account_expires_at': seat.account.expires_at,
                             'state': table.state(seat)}, headers=no_store)

    @router.post('/seats/{slot}')
    async def join(slot: int, request: Request):
        if slot not in (0, 1):
            raise QuizError('invalid_seat', '퀴즈 좌석을 선택해 주세요.')
        return admission(table.join(slot, await verified_account(request)))

    @router.post('/seat/renew')
    async def renew(request: Request):
        ticket = request.headers.get('x-quiz-seat-ticket', '')
        table.seat_for_ticket(ticket)
        return admission(table.renew(ticket, await verified_account(request)))

    @router.get('/state')
    async def state(request: Request):
        return JSONResponse(table.state(table.identify(request)), headers=no_store)

    @router.delete('/seat')
    async def leave(request: Request):
        table.leave(table.identify(request))
        return Response(status_code=204, headers=no_store)

    @router.post('/pdf')
    async def upload(request: Request, round_id: str, start_page: str | None = None, end_page: str | None = None):
        seat = table.identify(request)
        sheet = table.accept_upload(seat, round_id)
        if request.headers.get('content-type', '').split(';')[0].strip().lower() != 'application/pdf':
            raise QuizError('unsupported_file', 'PDF 파일을 선택해 주세요.', 415)
        if table.pending_uploads >= 2:
            raise QuizError('upload_busy', '자료를 확인 중입니다. 잠시 후 다시 제출해 주세요.', 429)
        table.pending_uploads += 1
        sheet.uploading = True
        try:
            # At most two seated upload handlers can wait. Serialize PDF parsing and model
            # work with the legacy quiz API on the same single-worker free instance.
            async with worker:
                table.identify(request)
                table.round.check_id(round_id)
                data = await read_body(request, settings.max_pdf_bytes)
                selected, _, first, last = await asyncio.to_thread(select_pdf_range, data, start_page, end_page)
            if len(selected) > settings.max_pdf_bytes:
                raise QuizError('file_too_large', '선택한 PDF 범위 용량이 제한을 초과했습니다.', 413)
            # A leave/replacement, expired ticket or new round may have arrived while reading.
            table.identify(request)
            table.round.check_id(round_id)
            sheet.pdf, sheet.pages, sheet.first_page = selected, last - first + 1, first
            sheet.questions = []
            sheet.applied_level = None
        finally:
            table.pending_uploads -= 1
            sheet.uploading = False
        if all(s.ready and not s.uploading for s in table.round.sheets):
            table.start_job('generating')
        return JSONResponse(table.state(seat), headers=no_store)

    @router.post('/answer')
    async def answer(request: Request):
        return await save_answer(request, Answer)

    @router.post('/draft')
    async def draft(request: Request):
        return await save_answer(request, Draft)

    async def save_answer(request, model):
        table.identify(request)
        data = await read_body(request, 8 * 1024)
        try:
            value = model.model_validate_json(data)
        except ValidationError as exc:
            raise QuizError('invalid_answer', '현재 문제의 답안을 확인해 주세요.') from exc
        seat = table.identify(request)
        table.submit(seat, value)
        return JSONResponse(table.state(seat), headers=no_store)

    @router.post('/retry')
    async def retry(request: Request, round_id: str):
        seat = table.identify(request)
        rd = table.round
        rd.check_id(round_id)
        if rd.phase == 'error' and all(s.ready and not s.uploading for s in rd.sheets):
            table.start_job(rd.failed_at)
        return JSONResponse(table.state(seat), headers=no_store)

    return router
