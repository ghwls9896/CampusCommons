"""Use the project's existing Render quiz service for a loopback match."""
import httpx

from quiz import AnswerSubmission, QuizError, validate_grades, validate_quiz


class HostedQuiz:
    ORIGIN = 'https://campuscommons-quiz.onrender.com'

    def __init__(self, access_token: str):
        # Accept only a quiz deployment token, never a Google API key.
        if (not isinstance(access_token, str) or not 1 <= len(access_token) <= 128
                or not access_token.isascii() or not access_token.isprintable()
                or ' ' in access_token or access_token.startswith(('AIza', 'AQ.'))):
            raise ValueError('유효한 퀴즈 서비스 연결 설정이 필요합니다.')
        self._token = access_token

    async def _post(self, client, path, **kwargs):
        # Never follow a redirect carrying the service credential, PDF, or answers.
        try:
            response = await client.post(self.ORIGIN + path, follow_redirects=False,
                                         headers={'X-Quiz-Token': self._token, **kwargs.pop('headers', {})},
                                         **kwargs)
        except httpx.RequestError as exc:
            raise QuizError('quiz_service_unavailable', '퀴즈 서버에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.', 503) from exc

        if response.is_success:
            try:
                data = response.json()
                if isinstance(data, dict):
                    return data
            except ValueError:
                pass
            raise QuizError('invalid_quiz_response', '퀴즈 서버의 응답을 확인하지 못했습니다.', 502)

        # Error text in remote/proxy responses may contain uploaded/request details.
        # Return only messages belonging to the known student-facing error contract.
        try:
            data = response.json()
            error = data.get('error') if isinstance(data, dict) else None
            code = error.get('code') if isinstance(error, dict) else None
        except ValueError:
            code = None
        messages = {
            'busy': '퀴즈 서버가 다른 요청을 처리 중입니다. 잠시 후 다시 시도해 주세요.',
            'daily_limit': '오늘 퀴즈 서버의 AI 사용 한도에 도달했습니다.',
            'quota_exceeded': 'Gemini 사용 한도에 도달했습니다. 잠시 후 다시 시도해 주세요.',
            'insufficient_material': '세 단계 문제를 만들 수 있는 학습 내용이 있는 PDF를 선택해 주세요.',
            'unauthorized': '퀴즈 서버 연결 설정을 확인해 주세요.',
            'provider_access': '퀴즈 서버의 AI 사용 권한을 확인해 주세요.',
            'model_unavailable': '퀴즈 서버에 설정된 모델을 사용할 수 없습니다.',
            'server_configuration': '퀴즈 서버의 출제 설정을 확인해 주세요.',
        }
        if isinstance(code, str) and code in messages:
            raise QuizError(code, messages[code], response.status_code)
        raise QuizError('quiz_service_unavailable', '퀴즈 서버가 요청을 완료하지 못했습니다. 다시 시도해 주세요.', 502)

    async def generate(self, pdf: bytes, pages: int, client: httpx.AsyncClient, target_level: int | None):
        params = {} if target_level is None else {'target_level': target_level}
        data = await self._post(client, '/v1/quizzes', headers={'Content-Type': 'application/pdf'},
                                content=pdf, params=params, timeout=240)
        page_range = data.get('page_range')
        expected_range = {'start_page': 1, 'end_page': pages, 'total_pages': pages}
        if (page_range != expected_range
                or any(type(page_range[key]) is not int for key in expected_range)):
            raise QuizError('invalid_quiz_response', '퀴즈 서버가 처리한 PDF 범위가 일치하지 않습니다.', 502)
        quiz = validate_quiz({'enough_material': True, 'reason': '', 'questions': data.get('questions')}, pages)
        if 'target_level' in data:
            applied = data['target_level']
            if type(applied) is not int or applied != target_level:
                raise QuizError('invalid_quiz_response', '퀴즈 서버의 적용 난이도를 확인하지 못했습니다.', 502)
            quiz['target_level'] = applied
        return quiz

    async def grade(self, answers: list[AnswerSubmission], client: httpx.AsyncClient):
        payload = {'answers': [answer.model_dump() for answer in answers]}
        response = await self._post(client, '/v1/quizzes/grade', json=payload, timeout=120)
        return validate_grades(response, answers)
