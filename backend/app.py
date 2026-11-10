"""One worker: uvicorn app:app --host 0.0.0.0 --port $PORT --workers 1."""
import asyncio
from dataclasses import dataclass
from datetime import datetime, timezone
import hmac
import os

import httpx
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from quiz import (QuizError, generate_quiz, grade_answers, pdf_page_count,
                  select_pdf_range, MAX_SELECTED_PAGES, GradingRequest, validate_grades)


@dataclass(frozen=True)
class Settings:
    api_key: str = ''
    access_token: str = ''
    model: str = 'gemini-3.5-flash-lite'
    daily_limit: int = 30
    max_pdf_bytes: int = 10 * 1024 * 1024
    upload_timeout: float = 30
    generation_timeout: float = 160
    grading_timeout: float = 45

    @classmethod
    def from_env(cls):
        return cls(api_key=os.getenv('GEMINI_API_KEY', '').strip(),
                   access_token=os.getenv('QUIZ_ACCESS_TOKEN', '').strip(),
                   model=os.getenv('GEMINI_MODEL', 'gemini-3.5-flash-lite').strip(),
                   daily_limit=max(1, int(os.getenv('DAILY_GENERATION_LIMIT', '30'))))


def create_app(settings: Settings | None = None, generator=generate_quiz, grader=grade_answers) -> FastAPI:
    settings = settings or Settings.from_env()
    api = FastAPI(title='Campus Commons PDF Quiz', docs_url=None, redoc_url=None, openapi_url=None)
    busy = False
    budget_day = ''
    used = 0

    @api.exception_handler(QuizError)
    async def quiz_error(_request, exc):
        return JSONResponse({'error': {'code': exc.code, 'message': exc.message}},
                            status_code=exc.status, headers={'Cache-Control': 'no-store'})

    @api.get('/healthz')
    async def health():
        return {'status': 'ok'}

    def authorize(request: Request, content_type: str):
        if not settings.api_key or not settings.access_token:
            raise QuizError('server_configuration', '관리자가 서버의 API 키와 참가 코드를 설정해야 합니다.', 503)
        if not hmac.compare_digest(request.headers.get('x-quiz-token', '').encode(), settings.access_token.encode()):
            raise QuizError('unauthorized', '참가 코드가 맞지 않습니다. 연결 설정을 확인해 주세요.', 401)
        if request.headers.get('content-type', '').split(';')[0].strip().lower() != content_type:
            raise QuizError('unsupported_file', '올바른 요청 형식이 아닙니다.', 415)
        if busy:
            raise QuizError('busy', '다른 학습 자료를 처리 중입니다. 잠시 후 다시 시도해 주세요.', 429)

    def use_budget():
        nonlocal budget_day, used
        day = datetime.now(timezone.utc).date().isoformat()
        if budget_day != day:
            budget_day, used = day, 0
        if used >= settings.daily_limit:
            raise QuizError('daily_limit', '오늘 서버의 AI 사용 한도에 도달했습니다. 다음 날 다시 이용해 주세요.', 429)
        used += 1

    async def read_body(request: Request, limit: int):
        data = bytearray()
        try:
            async with asyncio.timeout(settings.upload_timeout):
                async for chunk in request.stream():
                    if len(data)+len(chunk)>limit:
                        raise QuizError('file_too_large', '요청 크기가 허용 한도를 초과했습니다.', 413)
                    data.extend(chunk)
        except TimeoutError as exc:
            raise QuizError('upload_timeout', '업로드 시간이 초과되었습니다. 다시 시도해 주세요.', 408) from exc
        return bytes(data)

    @api.post('/v1/quizzes')
    async def quizzes(request: Request):
        return await process_pdf(request, inspect_only=False)

    @api.post('/v1/pdfs/inspect')
    async def inspect_pdf(request: Request):
        return await process_pdf(request, inspect_only=True)

    async def process_pdf(request: Request, inspect_only: bool):
        nonlocal busy
        authorize(request, 'application/pdf')
        busy = True
        try:
            data = await read_body(request, settings.max_pdf_bytes)
            if inspect_only:
                pages = await asyncio.to_thread(pdf_page_count, data)
                return JSONResponse({
                    'size_bytes': len(data), 'page_count': pages,
                    'max_size_bytes': settings.max_pdf_bytes, 'max_pages': MAX_SELECTED_PAGES,
                    'max_selected_pages': MAX_SELECTED_PAGES, 'supports_page_ranges': True,
                    'within_limits': True,
                }, headers={'Cache-Control': 'no-store'})
            selected, total, first, last = await asyncio.to_thread(
                select_pdf_range, data, request.query_params.get('start_page'), request.query_params.get('end_page'))
            if len(selected)>settings.max_pdf_bytes:
                raise QuizError('file_too_large', '선택한 범위의 PDF 용량이 제한을 초과했습니다.',413)
            use_budget()
            async with httpx.AsyncClient() as client:
                try:
                    async with asyncio.timeout(settings.generation_timeout):
                        result = await generator(selected, last-first+1, settings.api_key, settings.model, client)
                except TimeoutError as exc:
                    raise QuizError('generation_timeout', 'AI 응답이 오래 걸립니다. 다시 생성해 주세요.', 504) from exc
            for question in result['questions']:
                question['source_page'] += first-1
            result['page_range'] = {'start_page': first, 'end_page': last, 'total_pages': total}
            return JSONResponse(result, headers={'Cache-Control': 'no-store'})
        finally:
            busy = False

    @api.post('/v1/quizzes/grade')
    async def grades(request: Request):
        nonlocal busy
        authorize(request, 'application/json')
        busy = True
        try:
            data = await read_body(request, 48 * 1024)
            try:
                answers = GradingRequest.model_validate_json(data).answers
            except (ValidationError, ValueError) as exc:
                raise QuizError('invalid_answers', '채점할 답안을 확인해 주세요.') from exc
            if not any(a.answer.strip() for a in answers):
                result = {'grades': [{'level': a.level, 'correct': False, 'feedback': '제출된 답이 없습니다.'} for a in answers]}
            else:
                use_budget()
                async with httpx.AsyncClient() as client:
                    try:
                        async with asyncio.timeout(settings.grading_timeout):
                            result = await grader(answers, settings.api_key, settings.model, client)
                    except TimeoutError as exc:
                        raise QuizError('grading_timeout', '채점이 지연되고 있습니다. 결과 화면에서 다시 채점해 주세요.',504) from exc
            return JSONResponse(validate_grades(result, answers), headers={'Cache-Control': 'no-store'})
        finally:
            busy = False

    return api

app = create_app()
