# CC-8 계정·성장 데이터 인수인계

- Issue: https://github.com/ghwls9896/CampusCommons/issues/8
- PR: https://github.com/ghwls9896/CampusCommons/pull/10 (Draft)
- 담당 HIGHCHOI / 최종 사람 리뷰 ghwls9896
- 브랜치 codex/cc-8-account-progression / 기준 main fc5d8c7
- 폴더 CampusCommons-account-progression. 퀴즈/웹캠 브랜치와 분리.

## 구현
- Supabase Auth REST: 이메일 회원가입·로그인, 가입확인 코드, 코드 재전송, 비밀번호 복구·변경, 메모리 세션 갱신/로그아웃.
- 가입확인/복구 코드를 게임에서 입력하므로 별도 인증 웹사이트를 요구하지 않음.
- 닉네임2~12자/NFC/중복허용/직전 값 채움, DB UUID에 개인 성장 귀속.
- PostgreSQL profiles/player_progress/xp_events, Auth 신규 사용자 초기화·기존 사용자 보완, RLS·최소 권한.
- private 서버 검증 활동 기반 보상 저장소, 동시/중복 지급 방지 및 롤백. XP0/레벨1 시작, 레벨은 누적XP에서 generated 계산.
- Unity RuntimeInitialize/sceneLoaded gate로 기존 Player/GameStart/씬을 수정하지 않고 로그인 전 이동·컨트롤러/퀴즈 입력 차단. 로그아웃 시 탈출 활동 종료·세션 캐시 제거.
- Unity 메뉴 Campus → 계정 → Supabase 연결 설정. 공개 URL/key만 허용.

## 수정 경계
Assets/Account/, supabase/, Tools/account/, 계정 문서·task, UnityWebRequest 모듈 패키지만 변경. 공유 선언은 Packages/manifest.json, Packages/packages-lock.json. PR #4에도 동일 모듈 추가가 있어 최종 main 통합 때 패키지 중복을 확인. GameStart/Player/메인 씬/다른 기능 코드는 수정하지 않음.
별도 공유 파일 통합 PR 대신 모듈 내부 bootstrap으로 연결한 이유와 검증은 설계/진행 기록에 남김. 새 캐릭터나 룸 모듈 추가 시 차단 대상 호환성 확인 필요.

## 검증 (2026-11-10)
- 실제 임시 PostgreSQL14: 미인증/타인/미확인 계정 접근 거절, 초기화, 닉네임, 레벨 경계, 중복16회·동시 지급·응답 유실 재시도·overflow 롤백 통과.
- Edge Functions: Deno2.9.6 테스트9개, 두 entrypoint type check 통과. Auth/REST upstream은 fixture 응답.
- C# 순수 세션 테스트23 checks: 계정 전환 지연응답/세션 갱신 중복/복구 제한/로그아웃·해제/닉네임 검증.
- 임시 Unity6000.6.0f1: 스크립트 컴파일 및 실제 GrandLibrary_Living 씬의 ACCOUNT_FLOW_PASS. 기존 Player의 초기 입력 차단, 로그인 후 아바타/카메라 생성, 닉네임 저장, XP 유지, 로그아웃 차단 검증. 비동기 이전 닉네임 자동 입력·만료 후 오프라인 차단 회귀 검사 추가. Supabase 응답은 fixture.
- 알려진 Unity Editor SearchDatabase 시작 예외는 테스트에서 editor-only로 구분. 게임 런타임 오류는 통과 조건에 포함.
- 기존 협업 테스트16개 및 제출 범위44개 경로 검사 통과. 후속 회귀 수정 커밋은 동일 검사를 다시 실행.
- 전체 변경 별도 코드 검토 후 만료된 오프라인 세션의 입력 허용과 비동기 닉네임 자동 입력 문제를 각각 재현 후 수정. 최종 ACCOUNT_FLOW_PASS 및 세션23/Edge9 검사 통과. 최신 서버 named key/legacy key 양쪽 호환.

## 설정·미검증
- campuscommons hosted DB/함수·Unity 공개 연결값·Auth 정책·SMTP·코드 메일 템플릿 적용. 실제 수신/확인 상태는 아래 후속 기록과 docs/account/DEPLOYMENT.md 참고.
- 설정 순서: docs/account/SETUP.md. CLI3.0은 db push --remote 명시. Auth/SMTP Dashboard 설정은 local config가 자동 반영하지 않음.
- 기본 SMTP는 외부 학생에게 발송할 수 없음. 일반 사용자 가입확인/복구에는 custom SMTP와 발신 인증 필요.
- Docker 미실행: 전체 Supabase 로컬 Auth 스택 대신 실제 PostgreSQL14+역할 helper로 DB 검사. hosted PG/Advisor는 별도 확인.
- Unity6000.4.5f1은 설치돼 있지 않아 원본 버전 실행/standalone 빌드는 미검증. 원본 ProjectVersion은 유지.
- 실제 퀴즈/탈출/웹캠 보상은 아직 비활성. 서버가 결과를 검증하는 원천별 계약 후 ACCOUNT_REWARD_SOURCES에 해당 source만 활성화. 클라이언트 성공 플래그로 지급하지 않음.
- 운영 백업 export·복구 리허설은 실제 프로젝트 생성 후 운영자가 수행.

다음 행동: 사용자가 쓸 비밀번호 직접 설정·Unity 실제 Auth 화면 → 두 계정·지정 Unity 버전 확인 → 사람 리뷰/통합.

## Supabase 배포 진행 — 2026-11-10

사용자가 생성하고 Chrome에 연 campuscommons 프로젝트(ref gumkrzpnzaaxqlpswwvn)에 후속 설정을 위임함.
- 연결 Supabase MCP는 이 프로젝트 권한이 없어 로그인된 Chrome Dashboard로 진행.
- 기존 public 테이블이 비어 있음을 확인한 후 migration을 트랜잭션으로 적용. profiles/player_progress/xp_events의 rowsecurity=true, account_read/enter/award 함수 생성 확인.
- supabase_migrations.schema_migrations에 20261109081917/account_progression 적용 이력 기록 및 RLS 보호 확인. 같은 초기 migration을 다시 실행하지 않음.
- account와 progression Edge Functions를 배포해 각각 Settings 화면 확인. 공유 소스 파일을 단일 index.ts로 결합한 동일 코드. Verify JWT with legacy secret은 기본 ON 유지.
- 프로젝트 URL: https://gumkrzpnzaaxqlpswwvn.supabase.co
- Allow signup/Email provider/Confirm email 활성, anonymous 비활성 상태 확인.
- 최소 비밀번호12/OTP600초·6자리 입력 단계에서 Chrome의 다른 확장 UI가 자동조작을 차단. 저장 여부는 확인하지 못함.
- 남음: 인증 정책 저장 확인, 메일 템플릿·SMTP, 공개 key 조회·Unity 연결값 저장, 실제 가입/로그인 검증, 새 JWT 서명키와 Edge gateway 호환성 확인.
- 중단 원인: 다른 확장 프로그램 UI가 열려 Chrome 자동조작 차단. 사용자가 해당 UI를 닫으면 같은 Supabase 탭에서 계속.

### 배포 후속 완료 — 2026-11-10
Auth 최소12자/OTP600초·6자리 저장, Unity 공개 URL/key 연결, EC 키용 레거시 게이트 해제(사용자 승인)/함수 내부 Auth 검증 유지 완료. 실제 미로그인·위조 토큰 접근 거절 및 hosted Advisor 확인. 자세한 현황은 docs/account/DEPLOYMENT.md.
SMTP 일반값을 채운 Chrome 화면을 사용자에게 인계. 발신 Gmail/앱 비밀번호 입력·저장 후에만 메일 템플릿 및 실제 가입/복구 검사를 진행할 수 있음.

### SMTP·코드 메일 적용 — 2026-11-10
사용자가 SMTP 자격정보 직접 저장 후 새로고침으로 저장 상태 확인. Confirm sign up/Reset password에 저장소의 한국어 `{{ .Token }}` 본문 및 제목 저장 성공·미리보기 확인. 사용자 지정 수신 주소로 실제 signup HTTP200, 확인 전 세션 미발급 확인. 코드 확인 및 실제 로그인/DB 검증은 진행 중이며 주소·임시 비밀번호·OTP·토큰을 저장소에 기록하지 않음.

### 실제 메일·Auth·DB 확인 — 2026-11-10
가입 메일 수신·코드 확인200·비밀번호 로그인200·ES256 JWT·계정 함수 read/enter/read200·XP0/레벨1 확인. 임시 검사 도구의 닉네임 필드 오판(last_nickname과 API nickname 혼동)은 SQL의 저장 값·account_read 응답 일치=true로 대조했으며 제품 코드 문제는 아님. 인증된 클라이언트 XP 직접 수정403, 미검증 보상409, refresh200/logout204/로그아웃 refresh 재사용400 확인. 복구 메일 요청200 및 사용자 수신 확인. 검사 프로세스 종료, 비밀값·주소는 저장소에 기록하지 않음.
사용자는 테스트 계정을 직접 쓸 때 Unity의 비밀번호 찾기로 본인 비밀번호를 설정해야 함. 지정 Unity 버전·standalone·다른 PC/두 계정 검증과 사람 리뷰는 남아 있어 Draft 유지. 최신 코드 커밋48df1f2, 이번 후속 변경은 배포 기록 문서만 포함.
