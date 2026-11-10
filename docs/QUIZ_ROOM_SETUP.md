# PDF 퀴즈룸 사용 및 배포

Unity 데스크톱(Windows/macOS)에서 퀴즈룸 입장 → PDF 선택 → 문제 생성 → 레벨 1/2/3 각 1문항 → 정답·해설·근거 페이지 → 최종 결과 순서로 이용합니다. 4지선다이며 레벨은 기초 개념 / 적용 / 비교·추론입니다. PDF는 최대 10MiB, 80페이지입니다. 암호화·손상 PDF는 거절합니다. 이미지로 된 PDF도 Gemini에 직접 전달하므로 모델이 읽을 수 있는 스캔 품질이 필요합니다.

## 1. Google AI Studio: API 키 만들기

1. [Google AI Studio](https://aistudio.google.com/api-keys)에 Google 계정으로 로그인합니다.
2. 다른 서비스와 분리한 **CampusCommons 전용 새 프로젝트**를 만들고 그 안에 퀴즈 서버용 키를 생성합니다. 프로젝트의 **무료 등급** 표시를 확인하고 결제 계정/카드를 연결하지 않습니다.
3. [모델별 요금표](https://ai.google.dev/gemini-api/docs/pricing)에서 사용할 모델의 무료 등급을 확인합니다. 기본값은 `gemini-3.5-flash-lite`입니다. 2026-11-10 실제 무료 프로젝트에서 Render → Gemini → Unity의 PDF 3문항 생성/풀이를 확인한 모델입니다. 제공 모델·무료 한도는 변경될 수 있습니다. 해당 계정에 제공되지 않으면 PDF와 structured output을 지원하는 무료 모델로 Render의 `GEMINI_MODEL`만 변경합니다. 유료 모델로 자동 전환하거나 자동 재시도하지 않습니다.
4. [AI Studio의 사용량 및 한도](https://aistudio.google.com/usage)에서 프로젝트 한도를 확인합니다. 모든 플레이어가 이 프로젝트의 한도를 함께 사용합니다. 분당 요청·입력 토큰·일일 요청 중 하나라도 소진되면 생성이 실패할 수 있습니다.

API 키는 **Render 서버 환경변수에만** 넣습니다. Unity, GitHub, 채팅, 스크린샷에 넣지 마세요. 무료 Gemini에 보낸 내용은 Google의 제품 개선에 사용될 수 있으므로 본인이 공유할 수 있는 학습 자료를 사용합니다. [Gemini 이용 약관](https://ai.google.dev/gemini-api/terms)

## 2. Render: 무료 서버 배포

구현은 GitHub의 `codex/pdf-quiz-room` 브랜치와 [Draft PR #4](https://github.com/ghwls9896/CampusCommons/pull/4)로 공유되어 있습니다. 팀 리뷰 후 main에 합쳤다면 main을 배포 대상으로 선택합니다. 검토 중 기능을 테스트할 때는 해당 기능 브랜치를 선택합니다.

1. [Render](https://dashboard.render.com/)에 가입하고 GitHub 계정을 연결합니다.
2. **New → Blueprint**에서 `ghwls9896/CampusCommons` 저장소와 위 브랜치를 선택합니다. 루트의 `render.yaml`이 Python Web Service 한 개를 구성합니다. 반드시 인스턴스가 **Free**인지 확인합니다.
3. `GEMINI_API_KEY`에 AI Studio 키를 입력합니다. `QUIZ_ACCESS_TOKEN`은 Blueprint가 무작위로 생성합니다.
4. 배포 후 서비스 **Environment**에서 `QUIZ_ACCESS_TOKEN`을 확인합니다. 운영자가 아래 Unity 배포 설정에 넣을 **데모 접근 코드**입니다. Gemini API 키와 다릅니다. 플레이어에게 입력을 요구하지 않습니다.
5. 서비스 상단의 `https://이름.onrender.com` 주소를 복사합니다. 브라우저에서 `/healthz`를 붙여 `{"status":"ok"}`가 나오는지 확인합니다. 이 검사는 서버 실행만 확인하며 Gemini 인증·한도를 검증하지 않습니다.

Blueprint를 쓸 수 없거나 저장소가 보이지 않으면 저장소 관리자가 GitHub 앱 접근을 허용해야 합니다. 공개 저장소는 **New → Web Service → Public Git Repository**로 연결할 수도 있습니다. 수동 설정은 다음과 같습니다.

| 설정 | 값 |
|---|---|
| Runtime | Python |
| Root Directory | `backend` |
| Build Command | `pip install -r requirements.txt` |
| Start Command | `uvicorn app:app --host 0.0.0.0 --port $PORT --workers 1` |
| Instance Type | **Free** |
| Health Check Path | `/healthz` |
| `PYTHON_VERSION` | `3.14.2` |
| `GEMINI_API_KEY` | AI Studio에서 만든 비밀 키 |
| `QUIZ_ACCESS_TOKEN` | 직접 만든 긴 무작위 코드(공백 없는 ASCII, 128자 이내) |
| `GEMINI_MODEL` | `gemini-3.5-flash-lite` |
| `DAILY_GENERATION_LIMIT` | `30` |

데이터베이스·디스크·GPU 서버는 필요하지 않습니다. Render는 API 중계만 하고 AI 추론은 Gemini에서 수행합니다. 무료 서버는 15분 미사용 시 잠들며 다음 연결에 약 1분이 걸릴 수 있습니다. 월 750시간은 워크스페이스 전체가 공유합니다. 결제 수단이 있으면 대역폭·빌드 사용량 초과에 요금이 생길 수 있으므로 무료 사용만 원하면 결제와 사용량 설정을 확인하세요. [Render 무료 정책](https://render.com/docs/free)

## 3. 운영자가 한 번 할 Unity 연결 설정

`Assets/QuizRoom/Resources/QuizConnection.example.json`을 같은 폴더의 `QuizConnection.json`으로 복사합니다. `server_url`에는 실제 Render HTTPS 주소, `access_token`에는 Render의 `QUIZ_ACCESS_TOKEN` 값을 넣습니다. **GEMINI_API_KEY는 넣지 않습니다.** 실제 설정 파일과 해당 `.meta`는 Git에서 제외합니다.

이 설정은 게임 빌드에 포함되므로 플레이어는 PDF만 선택하면 됩니다. 데모 접근 코드는 배포 파일에서 추출 가능하며 계정 인증이나 비밀 보관 수단이 아닙니다. 서버의 기존 토큰 검사·생성 한도는 유지하며 추후 로그인 도입 시 사용자 세션으로 대체합니다. 새로 clone한 개발자는 운영자에게 설정을 별도로 받아야 하고, 설정 없는 빌드에서는 서비스 준비 중 안내가 표시됩니다.

## 4. 플레이어 사용

1. 저장소를 Unity Hub에 추가하고 프로젝트 버전 **6000.4.5f1**로 엽니다.
2. `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity`를 열고 Play합니다.
3. 로비에서 퀴즈룸으로 들어갑니다. 새 UI는 GameStart가 플레이어에 자동으로 연결하므로 씬 오브젝트를 직접 추가할 필요가 없습니다.
4. 서버에는 자동으로 연결됩니다. 서버 주소/참가 코드/API 키 입력칸은 표시하지 않습니다.
5. **PDF 선택하기**로 파일을 고르고 **문제 만들기**를 누릅니다. Unity Editor에서는 운영체제 파일 선택창, 데스크톱 빌드에서는 게임 내 파일 탐색기를 사용합니다.
6. 보기를 고르고 **답 제출**을 누르면 정답·해설·근거 페이지가 나옵니다. **다음 레벨**로 이동하여 총 3문항을 풉니다. 결과에서 전체 문항을 다시 볼 수 있습니다.
7. ESC 또는 **나가기**에서 확인하면 로비로 돌아가며 문제·파일 선택을 초기화합니다. 나가기 취소는 현재 풀이를 유지합니다. 요청 취소·방 나가기는 클라이언트 응답 수신을 중단하지만 이미 시작된 Gemini 호출은 한도를 소모할 수 있습니다.

다른 팀원도 설정이 포함된 같은 게임 빌드로 사용할 수 있습니다. 각자 Render나 AI Studio에 가입할 필요는 없습니다. 이번 구현은 Unity 데스크톱용이며 WebGL·모바일 파일 선택은 포함하지 않습니다.

## 제한과 오류 대응

- 한 번에 한 생성 요청만 처리합니다. 다른 사용자가 생성 중이면 잠시 후 직접 다시 시도합니다.
- 서버 자체 일일 한도는 기본 30회, UTC 자정(한국시간 오전 9시)에 초기화됩니다. 실패한 Gemini 호출도 집계합니다. 이 숫자는 **무료 Gemini 제공량이 아니라 앱 설정**입니다.
- 앱의 한도 카운터는 메모리에 있어 재시작·절전 후 초기화됩니다. 과금 차단 수단이 아닙니다. 무료 사용은 공급자 프로젝트의 무료 등급 설정으로 관리합니다. 여러 worker를 실행하면 한도가 분산되므로 반드시 `--workers 1`로 운영합니다.
- 무료 한도 오류는 Google 프로젝트의 실제 한도/재설정 시각을 확인합니다. 자동 재시도와 유료 모델 전환은 없습니다.
- PDF의 페이지 수·형식과 응답 구조를 검증하지만 정답의 사실성이나 인용문 일치까지 보장하지 않습니다. 학습자는 원문을 함께 확인해야 합니다.
- 문제와 PDF는 앱 서버의 메모리에서만 처리하고 파일/DB에 저장하지 않습니다. Gemini의 데이터 처리는 Google 정책을 따릅니다. 점수는 방을 나가거나 게임을 종료하면 사라집니다.
- 현재 공유 코드 방식은 소규모 팀 테스트용입니다. 공개 서비스와 향후 1:1 대결에는 사용자 인증, 영속 한도, 서버 채점, 매칭/동기화가 별도로 필요합니다.

## 개발 및 검증

```sh
cd backend
python3 -m venv .venv
.venv/bin/pip install -r requirements-dev.txt
.venv/bin/python -m pytest -q
```

서버 실행 시 셸에서 환경변수 2개를 설정하고 `.venv/bin/uvicorn app:app --host 127.0.0.1 --port 8000 --workers 1`을 실행합니다. `.env.example`은 설정 예시이며 자동으로 읽지 않습니다. 로컬 HTTP 서버는 API 개발용이고 Unity는 HTTPS 서버 주소만 허용합니다.

- 서버 테스트는 PDF·스키마·API 오류·동시 요청·한도 처리를 가짜 Gemini 응답으로 검증합니다. 실제 모델 호출이 아닙니다.
- `Tools/tests/QuizSessionTests.cs`는 순수 C# 채점/진행/검증 테스트입니다. `QuizData.cs`, `QuizConnection.cs`와 함께 컴파일해 실행합니다.
- `CampusCommons.Quiz.Editor.QuizRoomTest.Run`은 Unity Editor 배치 `-executeMethod` 진입점입니다. 실제 씬에서 입장·풀이·퇴장 상태를 검증합니다.
- 최종 연결 확인: 실제 API 키를 서버에 설정한 뒤 짧은 학습 PDF로 3문항 생성 → 제출 → 해설 → 결과까지 확인합니다. 빈 PDF·암호 PDF·잘못된 참가 코드도 화면에서 확인합니다. 키가 없는 환경에서 이 실제 AI 검증을 통과했다고 주장하지 않습니다.

API: `POST /v1/quizzes`, 헤더 `Content-Type: application/pdf`, `X-Quiz-Token: 참가 코드`, body는 PDF 원본 bytes입니다. 성공 시 `questions` 배열 3개, 실패 시 `error.code`, `error.message`를 반환합니다. 학생용 Unity에는 정답도 내려받아 로컬 채점합니다(현재 1인 복습 범위).

환경변수를 설정한 터미널에서 실제 배포 API를 직접 확인할 때는 다음과 같이 호출할 수 있습니다. 이 호출은 Gemini 한도를 사용합니다.

```sh
curl --fail-with-body "$QUIZ_SERVER_URL/v1/quizzes" \
  -H 'Content-Type: application/pdf' \
  -H "X-Quiz-Token: $QUIZ_ACCESS_TOKEN" \
  --data-binary @study.pdf
```
