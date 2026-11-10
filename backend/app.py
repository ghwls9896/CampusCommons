"""Run one worker: uvicorn app:app --host 0.0.0.0 --port $PORT --workers 1."""
import asyncio
from dataclasses import dataclass
from datetime import datetime, timezone
import hmac
import os

import httpx
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from quiz import QuizError, generate_quiz, pdf_page_count, validate_pdf


@dataclass(frozen=True)
class Settings:
    api_key: str = ''
    access_token: str = ''
    model: str = 'gemini-3.5-flash-lite'
    daily_limit: int = 30
    max_pdf_bytes: int = 10 * 1024 * 1024
    upload_timeout: float = 30
    generation_timeout: float = 160

    @classmethod
    def from_env(cls):
        return cls(api_key=os.getenv('GEMINI_API_KEY', '').strip(),
                   access_token=os.getenv('QUIZ_ACCESS_TOKEN', '').strip(),
                   model=os.getenv('GEMINI_MODEL', 'gemini-3.5-flash-lite').strip(),
                   daily_limit=max(1, int(os.getenv('DAILY_GENERATION_LIMIT', '30'))))


def create_app(settings: Settings | None = None, generator=generate_quiz) -> FastAPI:
    settings = settings or Settings.from_env()
    api = FastAPI(title='Campus Commons PDF Quiz', docs_url=None, redoc_url=None, openapi_url=None)
    # Demo limits are intentionally global, process-local and cleared on restart.
    busy = False
    budget_day = ''
    used = 0

    @api.exception_handler(QuizError)
    async def quiz_error(_request, exc):
        headers = {'Cache-Control': 'no-store'}
        return JSONResponse({'error': {'code': exc.code, 'message': exc.message}},
                            status_code=exc.status, headers=headers)

    @api.get('/healthz')
    async def health():
        # Render health checks must work before the operator configures credentials.
        return {'status': 'ok'}

    @api.post('/v1/quizzes')
    async def quizzes(request: Request):
        return await process_pdf(request, inspect_only=False)

    @api.post('/v1/pdfs/inspect')
    async def inspect_pdf(request: Request):
        # Inspect locally on the server: no Gemini request and no generation budget.
        return await process_pdf(request, inspect_only=True)

    async def process_pdf(request: Request, inspect_only: bool):
        nonlocal busy, budget_day, used
        if not settings.api_key or not settings.access_token:
            raise QuizError('server_configuration', '관리자가 서버의 API 키와 참가 코드를 설정해야 합니다.', 503)
        token = request.headers.get('x-quiz-token', '')
        if not hmac.compare_digest(token.encode(), settings.access_token.encode()):
            raise QuizError('unauthorized', '참가 코드가 맞지 않습니다. 연결 설정을 확인해 주세요.', 401)
        if request.headers.get('content-type', '').split(';')[0].strip().lower() != 'application/pdf':
            raise QuizError('unsupported_file', 'PDF 파일만 업로드할 수 있습니다.', 415)
        if busy:
            raise QuizError('busy', '다른 학습 자료를 처리 중입니다. 잠시 후 다시 생성해 주세요.', 429)
        busy = True
        try:
            data = bytearray()
            try:
                async with asyncio.timeout(settings.upload_timeout):
                    async for chunk in request.stream():
                        if len(data) + len(chunk) > settings.max_pdf_bytes:
                            raise QuizError('file_too_large', 'PDF 파일 크기는 10MB 이하여야 합니다.', 413)
                        data.extend(chunk)
            except TimeoutError as exc:
                raise QuizError('upload_timeout', 'PDF 업로드 시간이 초과되었습니다. 연결을 확인하고 다시 시도해 주세요.', 408) from exc
            if inspect_only:
                pages = await asyncio.to_thread(pdf_page_count, bytes(data))
                return JSONResponse({
                    'size_bytes': len(data), 'page_count': pages,
                    'max_size_bytes': settings.max_pdf_bytes, 'max_pages': 80,
                    'within_limits': pages <= 80,
                }, headers={'Cache-Control': 'no-store'})
            pages = await asyncio.to_thread(validate_pdf, bytes(data))
            day = datetime.now(timezone.utc).date().isoformat()
            if budget_day != day:
                budget_day, used = day, 0
            if used >= settings.daily_limit:
                raise QuizError('daily_limit', '오늘 서버의 문제 생성 한도에 도달했습니다. 다음 날 다시 이용해 주세요.', 429)
            used += 1  # failed upstream calls count too; no automatic paid fallback/retry
            async with httpx.AsyncClient() as client:
                try:
                    async with asyncio.timeout(settings.generation_timeout):
                        result = await generator(bytes(data), pages, settings.api_key, settings.model, client)
                except TimeoutError as exc:
                    raise QuizError('generation_timeout', 'AI 응답이 오래 걸립니다. 잠시 후 다시 생성해 주세요.', 504) from exc
            return JSONResponse(result, headers={'Cache-Control': 'no-store'})
        finally:
            busy = False

    return api


app = create_app()
