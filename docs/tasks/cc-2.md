# CC-2 퀴즈룸 인수인계

- 상태: 실제 무료 배포 및 Unity 전체 풀이 확인 완료. PR #4 최종 사람 리뷰/통합 대기이며 main에는 아직 미통합입니다.
- Issue: https://github.com/ghwls9896/CampusCommons/issues/2
- PR: https://github.com/ghwls9896/CampusCommons/pull/4
- 사람 담당: HIGHCHOI / 브랜치: codex/pdf-quiz-room / 최종 리뷰어: ghwls9896
- 기준 main fc5d8c7을 2f80bcc에서 통합했습니다. UI/LOW 요청 구현은472d58a, 이후 저장소 기본 모델/배포 설정을 실제 검증한 무료 Flash-Lite로 통일했습니다. 최종 커밋은 PR head에서 확인합니다.

## 최종 동작과 설정

- 플레이어: PDF 선택 → 문제 만들기 → 레벨1/2/3 각1문항 → 정답·해설·근거 페이지 → 최종 점수. 서버/키/참가 코드/파일 경로 입력을 표시하지 않습니다.
- 운영자: ignored Assets/QuizRoom/Resources/QuizConnection.json에 HTTPS 주소와 데모 접근 코드만 넣습니다. Gemini 키는 Render 환경변수에만 있습니다. 설정이 포함된 게임 빌드를 공유하며 개발 checkout의 설정은 별도로 전달합니다.
- 빌드에 포함된 데모 코드는 추출할 수 있어 계정 인증/비밀 보관 수단이 아닙니다. 서버의 기존 토큰 검사·동시 생성 제한·일일 한도는 유지합니다.
- 별도 CampusCommons Google 프로젝트(무료 등급)와 https://campuscommons-quiz.onrender.com Render Free를 사용합니다. 실제 운영 GEMINI_MODEL은 gemini-3.5-flash-lite입니다. 유료 fallback/자동 재시도는 없습니다.
- Render Billing에서 Hobby, No card on file, 서비스/빌드 현재 청구액$0.00 확인. 카드/유료 플랜/DB/GPU를 추가하지 않았습니다. 자동 배포는 껐습니다.

## 검증과 제한

- backend26 tests, C#27 checks, 협업16 tests 및 scope34개 경로 검사와 원격 Team checks 통과.
- 임시 Unity6000.6.0f1에서 최종 UI 컴파일/실제 씬 lifecycle 통과. 알려진 Editor SearchDatabase 인덱싱 예외는 런타임 오류와 구분했습니다. 원본 프로젝트6000.4.5f1 유지.
- 2026-11-10 직접 만든2쪽 학습 PDF를 실제 Unity 파일 선택창으로 열어 Render/Gemini 3문항 생성 → 레벨1/2 정답 → 레벨3 의도한 오답 → 각 해설/페이지 근거 → 최종2/3 → 새 PDF 초기화까지 수동 확인했습니다. 가짜 문제 주입이 아닌 실제 API 응답입니다.
- 초기3.8 Flash의 기본/LOW 요청은 ReadTimeout504가 재현되어 무료 Flash-Lite로 변경한 뒤 성공했습니다. 기록/설정에 실제 검증한 모델을 사용합니다.
- 별도 리뷰에서 파일 탐색의 접근 실패/다른 드라이브/긴 목록 문제를 고쳤고 재검토에 중요 잔여 지적이 없습니다. Windows/macOS standalone 및 정확한6000.4.5f1 검증은 아직 남았습니다.
- 테스트 화면은 /private/tmp/CampusCommons-quiz-validation에서 실행 중이며 사용자 PDF 선택 시작 화면으로 복귀했습니다. 사용자의 실제 학습 자료 자체를 대신 골라 업로드하지 않았습니다.

## 경계와 다음 행동

- 공통 접점: Player.cs의 퀴즈 입력/HUD 차단, GameStart.cs의 컴포넌트 부착, UnityWebRequest 패키지. 씬 구조는 변경하지 않았습니다. 기술 조율/최종 리뷰는 지정 담당 절차를 따릅니다.
- 계약: POST /v1/quizzes의 snake_case JSON을 QuizData.cs와 backend/quiz.py가 공유합니다.
- 기존 EscapeGame.cs CRLF-only 변경, 실제 설정/키/접근 코드/학습 PDF는 커밋에서 제외했습니다. 공유 파일 편집은 종료했고 이후 겹치는 작업은 PR #4와 조율합니다.
- 다음: 사용자 실제 학습 PDF 확인 → ghwls9896 최종 PR 리뷰/merge → 원래 Unity/standalone 추가 검증. 실제 상태는 Issue/PR을 기준으로 확인합니다.

## 2026-11-10 퀴즈 UI 개선

- macOS의 NFD 한글 파일명을 UI에서만 NFC로 정규화합니다. 실제 경로는 변경하지 않습니다.
- 전체 퀴즈 화면을 흰색 카드/남색 버튼으로 바꿨으며 이전 하단 전송 안내와 일반 페이지 제한 문구를 없앴습니다. 제한 초과는 선택 자료 바로 아래에 실제 값과 함께 표시합니다.
- 파일 용량 및 실제 페이지 수를 표시합니다. 10MiB 초과는 로컬 차단, 80페이지 초과는 생성 차단입니다. 검사한 파일 bytes를 그대로 생성에 사용해 파일 변경/잘못된 메타데이터 재사용을 방지합니다.
- `POST /v1/pdfs/inspect`는 기존 토큰/크기/형식/동시 요청 검사를 사용하며 Gemini를 호출하거나 생성 한도를 소비하지 않습니다. 새 클라이언트 전에 서버를 배포해야 합니다.
- 파일 검사/업로드/생성 상태를 애니메이션과 실제 경과 시간으로 표시하고 취소할 수 있습니다.
- backend29 tests, C# 문서16 checks 및 세션27 checks, 협업16 tests 통과. 임시 Unity6000.6.0f1 실제 씬 lifecycle PASS 및 빈 화면/한글 파일/페이지·용량 초과/생성 중/정답 해설/결과의 fixture 시각 검증 완료. 지정6000.4.5f1·standalone은 계속 미검증입니다.
- 별도 코드 검토에서 중요 결함 없음. 연결 취소 안내를 상태에 맞게 수정했습니다. 실제 사용자 학습 PDF를 에이전트가 대신 선택하거나 전송하지 않았습니다.

- 운영 확인: 4c1dfd8을 Render에 수동 배포했고 Live 상태 확인. 실제 Unity 파일 선택창에서 승인된 2쪽 샘플 선택 → 3.7KB/2페이지 표시 → 실제 Gemini 3문항 생성 → 정답2/오답1 및 해설/근거 → 최종2/3 → 새 PDF 빈 선택 화면 초기화까지 확인했습니다. 자동 배포/요금제/키 변경 없음.
