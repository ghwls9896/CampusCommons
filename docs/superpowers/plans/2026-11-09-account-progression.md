# Account and Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 이메일 계정으로 로그인하고 입장 닉네임을 저장하며 재접속해도 개인 XP·레벨을 복원한다.

**Architecture:** Supabase Auth가 이메일/비밀번호·OTP·세션을 담당한다. PostgreSQL RLS로 개인 데이터를 격리하고 서버 검증 보상만 트랜잭션으로 반영한다. Unity 계정 모듈은 기존 게임 코드와 분리해 먼저 검증한다.

**Tech Stack:** Unity6000.4.5f1 C#, UnityWebRequest/JsonUtility, Supabase Auth REST, PostgreSQL, Edge Functions TypeScript.

**Spec:** docs/superpowers/specs/2026-11-09-account-progression-design.md

상태: 구현 전 검토용 초안. 문서에 제안으로 표시된 닉네임·성장 곡선·세션 정책은 검토 후 구현한다. 실제 DB/메일 계정 설정 및 게임 보상 연동은 아직 수행하지 않았다.

## Global Constraints
- Unity 버전6000.4.5f1 유지.
- 이메일+비밀번호, 입장 시 닉네임 입력, Supabase Auth+PostgreSQL.
- 무료 요금제 우선. SMTP 발신 도메인/용량/휴면/백업 제약은 설계 문서 기준.
- 비밀번호·토큰 PlayerPrefs 저장 금지, service_role/secret key는 서버만 보관.
- 초기 XP0/레벨1, 최소 누적 XP=50*(L-1)*L 제안. 실보상 원천 계약은 후속 단계.
- GameStart/Player/씬/Packages는 공유 조율과 task scope 갱신 후 별도 통합 PR에서 수정.
- 현재 PR은 문서 전용. 아래 파일은 향후 구현 예정 경로이며 지금 만들지 않는다.

## Review Focus
- 이메일 미확인/재전송 제한/복구코드 만료 시 게임 입장 차단과 재시도 안내.
- 같은 PC 계정A→B 전환 후 A의 지연 응답/캐시가 B의 프로필을 덮어쓰지 않음.
- 중복 완료/응답 유실/동시 보상에서 XP가 정확히 한 번 누적됨.
- 다른 계정 user_id 대입/직접 DB·RPC 호출로 개인정보·XP 접근 불가.
- 인터넷 단절/프로필 초기화 실패/이미 씬에 존재하는 Player에서도 인증 전 입장 불가.

## Task 1: DB와 권한·성장 규칙
Files: supabase/config.toml, CLI로 생성한 supabase/migrations/*, supabase/tests/database/account_progression.test.sql, docs/account/SETUP.md.
Interfaces: profiles(user_id,last_nickname), player_progress(user_id,total_xp,level), xp_events(user_id,source,source_event_id,delta,policy_version); 내부 전용 award_verified_event(user_id uuid,source text,event_id uuid,delta bigint,policy_version int). 외부 클라이언트에서 이 내부 함수를 호출할 권한을 부여하지 않는다.
- [ ] Supabase CLI 설치 여부/버전·`--help`와 Docker 실행 조건을 확인하고 로컬 개발 스택을 준비한다. hosted 기존 프로젝트가 있다면 변경 전에 테이블/정책을 조사한다.
- [ ] SQL 역할 테스트 작성: 미인증/타인 조회/직접 XP 쓰기/내부 RPC 호출 거부, 가입 시 XP0/레벨1 초기화, 초기화 실패 롤백, XP99/100/299/300/600→레벨1/2/2/3/4, 동일 이벤트2회와 동시 별도 이벤트2개의 정확한 합산.
- [ ] 로컬 `supabase test db`로 필요한 객체 부재 실패를 확인하고 최소 테이블/GRANT/RLS/초기화 트리거/generated level/보상 트랜잭션 구현. 편집은 execute_sql 또는 db query로 반복한다. privileged 함수는 내부 schema/서버 전용 권한과 고정 search_path로 한정한다.
- [ ] 실제 anon/authenticated/서버 역할로 테스트 통과 확인. timestamp·unique event·nonnegative/overflow 제한·동시성까지 확인하고 advisors를 실행한다.
- [ ] migration은 CLI `supabase migration new`로 만들고, 로컬 변경을 db pull로 정리하는 CLI 지원 흐름을 확인해 기록한다. 깨끗한 로컬 DB에 재적용 후 테스트하고 해당 파일만 커밋한다. 명령은 사용 중 CLI help 기준으로 검증한다.

## Task 2: 계정 입장·성장 서버 접점
Files: supabase/functions/account/index.ts, supabase/functions/progression/index.ts, supabase/functions/_shared/auth.ts, supabase/functions/tests/account_test.ts, supabase/functions/tests/progression_test.ts.
Interfaces:
- POST account `{action:"enter",nickname}` + Bearer JWT → `{user_id,nickname,total_xp,level,next_level_total_xp}`.
- POST progression `{source,activity_id}` + Bearer JWT → `{total_xp,level,next_level_total_xp,awarded_xp,already_awarded}`.
- 인증은 검증된 JWT의 sub, 요청의 user_id/delta 필드는 거부. 활동 소유권과 완료를 서버 저장 상태에서 검증.
- [ ] Deno 테스트로 만료/위조 JWT, 미확인 계정, 타인 활동, 미완료 활동, 임의 delta, 잘못된 닉네임, 중복 활동의 예상 응답을 정의하고 실패를 확인한다.
- [ ] Auth 검증 및 단일 이벤트 트랜잭션 호출을 구현한다. 초기에는 보상 제공자를 비활성 상태로 두고 명시적 서버 테스트 fixture만 사용한다. 실제 배포에서 test reward route가 열리지 않게 테스트한다.
- [ ] account 입력은 NFC/trim 후 코드포인트2~12개, 제어문자/줄바꿈/태그를 거부한다. 현재 JWT 사용자만 수정하고 프로필 저장 실패를 성공으로 응답하지 않는다.
- [ ] `deno test --allow-env --allow-net supabase/functions/tests/` 및 실제 로컬 Auth+DB 통합 검사로 응답 유실 재시도/동시 요청/권한 거부를 확인한다. 테스트 환경을 명시하고 mock만 통과한 결과와 구분한다.
- [ ] public key/서버 secret 분리를 점검하고 서버 모듈만 커밋한다. 실보상 활성화는 Task5 이후 원천별 계약 완료 때 진행한다.

## Task 3: Unity 인증·세션 모듈
Files: Assets/Account/Scripts/{AccountModels,SupabaseAuthClient,AccountSession,AccountApiClient}.cs, Assets/Account/Editor/AccountSessionTests.cs, Tools/account/SessionTests.cs 및 모든 새 에셋/폴더 .meta.
Interfaces: `Task<AuthResult> SignUpAsync(string email,string password,CancellationToken ct)`, `SignInAsync(...)`, `VerifySignupCodeAsync(email,code,ct)`, `BeginPasswordRecoveryAsync(email,ct)`, `VerifyRecoveryCodeAsync(email,code,ct)`, `UpdatePasswordAsync(password,ct)`, `RefreshAsync(ct)`, `SignOutAsync(ct)`. AccountSession은 UserId/세션 세대/토큰을 메모리에서 소유한다. AccountApiClient.EnterAsync(nickname,ct)는 Task<EntryProfile>을 반환한다.
- [ ] C# 테스트: 로그인 실패·OTP 만료·토큰 갱신 실패·로그아웃 도중 응답·A→B 계정 전환·이중 제출·통신 시간초과의 상태 전이를 정의해 실패 확인.
- [ ] UnityWebRequest 비동기 어댑터와 snake_case DTO, 요청 취소/해제, 메모리 토큰, 갱신 중복 방지, 세션 세대 검사를 구현한다. 비밀번호/토큰은 로그에 출력하지 않는다.
- [ ] 테스트용 가짜 transport에서 모든 상태 검사를 통과시키고 실제 로컬 Auth에서 두 계정의 로그인·확인·복구를 검증한다.
- [ ] Unity6000.4.5f1 batch compiler 및 `CampusCommons.Account.Editor.AccountSessionTests.Run` 결과를 확인한다. editor path는 로컬 설치에서 찾아 사용하며 하드코딩하지 않는다.
- [ ] `.meta`와 GUID를 확인해 모듈만 커밋한다. 타 Unity 버전 검사 시 버전 차이를 기록한다.

## Task 4: 가입·로그인·닉네임·내 성장 화면
Files: Assets/Account/Scripts/{AccountGate,AccountView,ProgressHud}.cs, Assets/Account/Editor/AccountFlowTests.cs, Assets/Account/Scenes/AccountSandbox.unity 및 .meta.
Interfaces: AccountGate.State = SignedOut/Authenticating/EmailVerificationRequired/LoadingProfile/ChoosingNickname/Entering/InWorld; 인증·닉네임 저장 완료 시에만 EntryReady(EntryProfile) 이벤트 발생. 로그아웃/세션 무효화 시 EntryRevoked 발생.
- [ ] 독립 테스트 씬에서 코드 확인 전 진입 불가, 잘못된 이메일/비밀번호/닉네임, 닉네임 변경 후 동일 XP, 이전 계정 응답 무시, 서버 저장 실패의 UI 상태를 검증하는 테스트 작성/실패 확인.
- [ ] 로그인/가입/이메일 코드/복구/닉네임 화면과 로딩·재시도 UI 구현. 닉네임 직전 값 미리 채움, 패스워드 마스킹, 클릭 중복 방지. 모든 게임 입력은 gate 허용 시에만 활성.
- [ ] HUD에 닉네임·레벨·현재 레벨 내 XP/다음 필요 XP 표시. 예: totalXP150이면 레벨2, 50/200. 클라이언트 계산보다 서버 응답을 우선한다.
- [ ] 지정 Unity Play Mode에서 새 계정→입장→종료→재로그인, 다른 계정과 다른 PC, 한글 닉네임·빈 입력·네트워크 차단을 확인한다. 실제 SMTP 테스트와 로컬 Mailpit 결과를 구분한다.
- [ ] 독립 모듈·테스트 씬만 커밋. 아직 본 게임 공유 씬 연결은 하지 않는다.

## Task 5: 본 게임 통합과 인수인계
Files: 별도 통합 Issue/task에 GameStart.cs, 필요 최소 Player.cs, GrandLibrary_Living.unity, Packages/manifest.json·lock의 실제 수정만 선언; docs/account/SETUP.md 및 task 인수인계.
Interfaces: GameStart가 AccountGate.EntryReady를 구독해 Player 생성/활성화. EntryRevoked에는 입력/포털/HUD/활동 종료와 계정 화면 복귀. PR #4의 QuizRoom 부착 기능을 최신 main 기준으로 보존한다.
- [ ] 퀴즈 PR #4 및 공간/캐릭터 담당과 수정 지점을 확인하고, 최신 main에서 독립 통합 브랜치를 만든다. 본 계획 브랜치를 여러 기능이 쌓이는 장기 브랜치로 쓰지 않는다.
- [ ] 기존 Player가 씬에 있는 경우와 런타임 생성되는 경우 모두 로그인 전 이동·카메라·포털·좌석·탈출 입력이 막히는 재현 검사 작성.
- [ ] 작은 bootstrap 연결을 구현하고 로그인·계정 전환·로비 재입장·퀴즈/탈출 기존 동작을 지정 Unity 및 대상 standalone 빌드에서 검증한다.
- [ ] 보상 원천마다 서버 발급 활동ID/소유권/완료 근거/보상표/상한/재시도 계약을 문서화한다. 로컬 결과만 있는 기능은 XP 지급을 켜지 않는다. 이 항목은 계정 MVP와 별도 후속 PR로 완료 가능하다.
- [ ] 문서의 실검증/미검증/SMTP·무료 용량·백업 복구 절차를 갱신. `python3 Tools/team/check.py --base origin/main`, CI, ghwls9896 리뷰 후 인계한다.
