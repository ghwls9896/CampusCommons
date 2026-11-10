# CC-2 퀴즈룸 인수인계

- Issue: https://github.com/ghwls9896/CampusCommons/issues/2
- 사람 담당: HIGHCHOI / 브랜치: codex/pdf-quiz-room
- 핵심 구현 커밋: 30873a0. 이후 협업용 task JSON과 이 문서를 추가했습니다.
- 상태: 원격 브랜치와 Draft PR #4 게시. 공통 파일 사람 리뷰 및 실제 계정 생성 검증 대기. https://github.com/ghwls9896/CampusCommons/pull/4
- 완료: PDF 선택/업로드, Gemini 서버 호출, 레벨별 3문항, 제출 후 해설/근거 페이지/점수, Render Free 배포 설정.
- 공유 파일: Player.cs의 퀴즈 입력 차단/HUD, GameStart.cs의 컴포넌트 부착, UnityWebRequest 패키지. Issue #2에 조율 범위를 기록했고 GongBeak/ghwls9896의 승인이나 다른 로컬 작업과 충돌 없음은 아직 확인하지 않았습니다. 파일 잠금을 획득한 상태가 아닙니다.
- 계약: POST /v1/quizzes의 snake_case JSON을 QuizData.cs와 backend/quiz.py가 공유. Gemini 키는 서버에만 보관.
- 검증: backend 26 tests, C# 20 checks, 임시 Unity 6000.6.0f1 컴파일 및 QuizRoomTest. 원본은6000.4.5f1 유지. 네트워크 취소 검사는 미전송 요청 정리 범위만 포함.
- 미검증: 실제 Gemini 응답/무료 등급, Render 배포, Unity6000.4.5 및 Windows/macOS standalone 빌드.
- 다음 행동: 협업 설정 Issue #1의 PR을 먼저 통합 → 깨끗한 퀴즈 작업 폴더에서 origin/main을 merge → team-checks와 관련 Unity 검사 → 공통 파일 리뷰 → 실제 계정 설정 후 PDF 생성 확인.
- 외부 설정: GEMINI_API_KEY, QUIZ_ACCESS_TOKEN, GEMINI_MODEL. 비밀값은 docs/QUIZ_ROOM_SETUP.md에 따라 Render에서 설정.
- main 또는 다른 기능 브랜치에 자동으로 합치지 않았습니다. 미관련 EscapeGame.cs CRLF-only 변경은 이 작업에 포함하지 않았습니다.

- 협업 전환: 최신 main(eb786c2)의 공간/조명 변경을 통합합니다. 기존 테스트 증거는 핵심 구현 시점 기준이며, 최신 main 통합 후 Unity 검사는 별도로 필요합니다. Tools/ 대소문자를 최신 저장소와 통일했습니다. 협업 PR #3을 먼저 main에 합친 뒤 새 검사를 이 브랜치에도 반영합니다.

## 실제 테스트 준비 및 화면 수정 (2026-11-10)

- 최신 협업 main(fc5d8c7)을 2f80bcc에서 통합했고 .gitignore 충돌은 두 쪽의 제외 규칙을 유지해 해결했습니다.
- 사용자 요청으로 플레이어의 서버 주소/참가 코드/경로 입력을 제거했습니다. PDF 선택 → 문제 만들기만 표시하며 서버에는 자동 연결합니다. Editor는 OS 파일 선택창, standalone은 드라이브/전체 목록 페이지 이동을 지원하는 탐색기를 사용합니다.
- 운영자는 git-ignored `Assets/QuizRoom/Resources/QuizConnection.json`에 Render 주소와 데모 접근 코드를 넣습니다. Gemini 키는 Render에만 있습니다. 빌드에 포함되는 데모 코드는 사용자 인증/비밀 보관 수단이 아닙니다.
- 별도 CampusCommons Google 프로젝트(무료 등급)와 Render Free 서버 https://campuscommons-quiz.onrender.com 배포 완료. 결제 수단/유료 플랜을 추가하지 않았고 자동 배포는 껐습니다.
- 검증: C# 27 checks, backend 26 tests, 팀 도구 16 tests 통과. 임시 Unity6000.6.0f1 최종 코드 컴파일 및 실제 씬 입장/풀이/퇴장 `PDF_QUIZ_ROOM_PASS`. 알려진 Editor SearchDatabase 인덱싱 예외는 게임 런타임 오류와 구분합니다. 원본6000.4.5f1 유지.
- 별도 UI/연결 코드 검토에서 발견한 폴더 접근 실패/다른 드라이브/300개 이후 파일 선택 문제를 수정했고 정적 재검토에 중요 잔여 지적은 없습니다. Windows/macOS standalone 빌드 검증은 아직 남아 있습니다.
- 실제 서버 healthz는200. 직접 만든 2쪽 학습 PDF의 첫 실제 생성 요청은 Gemini 응답 timeout(504)으로 실패했습니다. 무료 모델의 LOW thinking 요청과 안전한 timeout 유형 로그를 추가해 재배포 후 재검증합니다. 실제 생성 성공으로 보고하지 않습니다.
