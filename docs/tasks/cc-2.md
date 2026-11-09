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
