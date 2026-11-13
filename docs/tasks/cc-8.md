# CC-8 로그인·회원가입 및 계정별 Lv.1~15 인수인계

- Issue: https://github.com/ghwls9896/CampusCommons/issues/8
- Draft PR: https://github.com/ghwls9896/CampusCommons/pull/10
- 담당 HIGHCHOI / 최종 사람 리뷰·통합 ghwls9896
- 브랜치 codex/cc-8-account-progression / 시작 기준 main fc5d8c7 / 최신 main 0a6c3f7 통합 8b70f32
- 작업 폴더 CampusCommons-account-progression. 다른 기능 worktree와 분리.
- 마지막 변경: 이 문서를 포함하는 독립 레벨/인증 마무리 커밋. Unity 구현 커밋 e1f4d10, DB 구현 fad8734, 승인 범위 문서 f17dbf5. 최종 head는 PR을 기준으로 확인.

## 완료한 구현
- 이메일+비밀번호 가입/로그인,6자리 가입확인 코드/재전송,복구 코드/새 비밀번호,세션 갱신,로그아웃.
- 닉네임2~12자/NFC/중복허용/직전 값 자동 입력. 계정 UUID 기준 DB 저장.
- 신규 계정 Lv.1, DB에서 독립 정수1..15 저장·조회. 닉네임 변경·재로그인으로 초기화되지 않음. Unity는 XP 없이 Lv 표시.
- 경험치 계산식과 레벨 분리. 기존 레벨/XP/이력 보존, 기존 범위 밖 레벨은 자동 보정 없이 migration 실패.
- RLS 본인 조회,클라이언트 레벨 수정 금지,서버의 확인된 Auth ID로만 계정 RPC 실행.
- 경험치 지급은 Edge409/DB55000으로 비활성. ACCOUNT_REWARD_SOURCES를 설정해도 지급 불가.
- RuntimeInitialize/SceneLoaded로 기존 Player/GameStart/씬을 수정하지 않고 로그인 전 입력 차단,로그아웃 시 로컬 활동 종료와 캐시 제거.
- Supabase 실제 프로젝트의 DB migration/이력,Auth 정책,SMTP/코드 메일,공개 연결값 적용. 상세 배포 현황은 docs/account/DEPLOYMENT.md.

## 검증 — 2026-11-12
- PostgreSQL14 실제 역할/RLS: 계정 분리,기본1,관리자5/10/15,범위 밖/null 거절,XP 독립,닉네임 보존,보상 차단 통과. migration 전후 모든 기존 테스트 행 비교 및 범위 밖 기존 레벨의 전체 롤백 통과.
- Deno2.9.6 Edge9 tests,두 entrypoint type check 통과. upstream Auth/REST는 fixture.
- Mono C# 세션42 checks: 인증/복구/계정 전환 지연응답,갱신,1/5/10/15 응답,범위 밖 거절,복구 중 입장/실패한 암호 변경 제한 통과.
- 임시 Unity6000.6.0f1 실제 GrandLibrary_Living 씬 ACCOUNT_FLOW_PASS: 초기 입력 차단,로그인 후 아바타/카메라,비동기 이전 닉네임 자동 입력,Lv15 보존,만료 후 오프라인 차단,로그아웃. Supabase 응답은 fixture.
- 알려진 Unity Editor SearchDatabase 시작 예외는 기존 테스트의 editor-only 필터로 구분. 게임 런타임 오류는 통과 조건에 포함.
- 최신 main0a6c3f7 통합 후 임시 Unity 실제 씬 검사도 ACCOUNT_FLOW_PASS.
- 독립 코드 리뷰 Critical/Important/Minor 없음, C#42개 독립 재실행 통과.
- 기존 협업 검사16개 통과. 제출 범위 TEAM_CHECK_PASS48 경로 통과.
- Hosted DB 기존1계정/Lv1 보존,독립 column/default1/RLS3개 확인. 트랜잭션으로 Lv15 저장/조회,0·16 거절,보상 차단 검사 후 ROLLBACK하여 실회원 데이터 유지.
- 2026-11-10 실제 이메일 수신·가입확인·비밀번호 로그인·닉네임 저장·refresh/logout 검증 및 복구 메일 수신 확인. 이번에 사용자 암호 변경을 실제 수행했다는 의미는 아님.

## 수정 경계와 공유 파일
Assets/Account/,supabase/,Tools/account/,계정 문서/task,기존 UnityWebRequest 모듈 패키지 추가. 이번 후속 작업에서 공유 씬/Player/GameStart/패키지를 추가 변경하지 않음. main의 캐릭터·탈출룸 통합 후 packages-lock 충돌은 ui/ugui와 unitywebrequest를 모두 보존하여 해결. 독립 모듈 bootstrap으로 연동하며 신규 캐릭터/룸은 차단 대상 호환성 검사가 필요함.

## 남은 검증과 다음 행동
- 지정 Unity6000.4.5f1이 설치되지 않아 해당 버전 및 standalone 빌드는 미검증. 원본 버전 설정 유지.
- 사용자는 기존 테스트 계정을 쓰려면 게임의 비밀번호 찾기에서 원하는 새 비밀번호를 직접 설정. 채팅/저장소에 비밀번호를 전달하지 않음.
- 실제 Unity 화면에서 두 계정/다른 PC/복구 후 새 비밀번호 로그인 최종 확인.
- 경험치 자동 상승,퀴즈 점수·시간 가중치,재화,5·10·15레벨 외형 보상은 별도 작업.
- 운영자 백업/복구 리허설 및 최종 사람 리뷰·통합은 남아 있으므로 Draft 유지. 자동 merge하지 않음.

## 시작 화면 후속 — 2026-11-12
사용자 요청으로 로그인/회원가입을 세로 한 열의 큰 선택 버튼으로 분리하고 하단에 비밀번호 찾기 배치. 로그인 전 배경 전용 카메라(cullingMask0)를 사용하여 No cameras rendering 오버레이 제거. 입장 시 배경 카메라 비활성, 로그아웃 시 복원. 공유 씬/Player 변경 없음. 임시 Unity6000.6 실제 씬에서 시작 페이지·카메라·입장·Lv15·로그아웃 회귀 ACCOUNT_FLOW_PASS.
