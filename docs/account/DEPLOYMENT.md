# campuscommons 실제 배포 현황 — 2026-11-12

프로젝트 ref: gumkrzpnzaaxqlpswwvn / API URL: https://gumkrzpnzaaxqlpswwvn.supabase.co
사용자가 만든 Free 프로젝트에 Chrome Dashboard에서 설정. Supabase MCP 연결에는 이 프로젝트 권한이 없어서 로그인된 UI로 수행.

## 2026-11-12 독립 레벨 전환
- 신규 migration 20261112013634/standalone_account_level을 hosted DB에 트랜잭션 적용하고 schema_migrations 이력도 같은 트랜잭션에 기록.
- 적용 전 회원 1명, 레벨 범위1..1, 범위 밖0. 적용 후 회원1명/레벨1 그대로, level is_generated=NEVER/default=1, public 세 테이블 RLS 유지.
- 실제 DB에서 임시 Lv.15 설정 → account_enter로 닉네임 저장 → 레벨15/XP 없는 응답 검사,0/16 거절, public/private 보상 함수55000을 검증하고 전체 테스트 트랜잭션 ROLLBACK. 실제 회원의 레벨·닉네임은 그대로 유지. 결과 HOSTED_LEVEL_CHECK_PASS.
- account/progression 함수의 공통 모듈과 entrypoint를 단일 파일로 결합하여 재배포. Dashboard 목록에서 두 함수의 새 배포 시각 확인. 실제 두 endpoint의 미로그인·위조 bearer 요청 모두401 unauthorized 검증.
- Unity DTO/HUD는 레벨만 사용. 새 가입 Lv.1, 최대15, 자동 상승·재화·외형 아이템 지급 없음.
- 로컬 PostgreSQL migration 데이터 보존/실패 롤백·권한 테스트 통과, Edge9/세션42/팀16 검사 통과.
- 임시 Unity6000.6.0f1 실제 메인 씬 fixture 검사 ACCOUNT_FLOW_PASS. 최초 라이선스 채널 연결 실패 후 기존 라이선스 IPC 채널 지정으로 정상 실행. 원본6000.4.5f1은 변경하지 않음.
- 아래 2026-11-10의 실메일/실제 로그인 결과는 당시 검증 기록이며, 이번 레벨 변경의 hosted 검증은 SQL 계약 검사와 공개 HTTP 보호 검사로 구분한다.

## 2026-11-10 완료·확인
- public.profiles/player_progress/xp_events, private 활동·함수, 신규 Auth 계정 초기화 적용.
- 세 public 테이블 rowsecurity=true와 account_read/account_enter/account_award 생성 확인.
- supabase_migrations.schema_migrations에 20261109081917/account_progression 적용 이력 기록. 이력 테이블도 RLS 활성. 같은 migration 재실행 불필요.
- account/progression 함수 배포. 소스의 공통 handler/dependency와 entrypoint를 단일 index.ts로 결합한 동일 코드.
- Allow signup/Email provider/Confirm email ON, Anonymous OFF.
- 최소 비밀번호12, OTP600초·6자리 저장 완료.
- Unity AccountConnection.json에 이 프로젝트 URL과 공개 publishable key 저장. 비밀키는 읽거나 저장하지 않음.
- 공개 /auth/v1/settings 실제200, email_provider=true/email_autoconfirm=false/signup_disabled=false 확인.
- 공개 JWKS는 EC signing key. 사용자에게 레거시 전용 게이트 변경을 설명하고 진행 승인을 받은 후 두 함수의 Verify JWT with legacy secret OFF. 함수 내부 Auth /user 호출 검증을 유지하고 local config에도 반영.
- 게이트 변경 후 실제 account/progression의 미로그인·위조 bearer 요청 각각401 {error:{code:unauthorized}} 확인.
- Hosted Health Advisor 오류0/경고0. Security Advisor 오류0/경고0/Info1: private.activity_runs RLS enabled no policy. 클라이언트 접근을 허용하지 않는 서버 전용 테이블이므로 의도된 deny-by-default 설정.
- 서버 코드 Deno9 tests 통과.

## SMTP·메일 템플릿 후속 설정
- 사용자가 발신 Gmail/Google 앱 비밀번호를 직접 입력·저장함. Dashboard를 새로고침한 후 custom SMTP ON, smtp.gmail.com:587, 저장된 비밀번호 안내 및 비활성 Save changes를 확인. 자격정보를 코드·저장소로 복사하지 않음.
- Confirm sign up 제목 `Campus Commons 이메일 인증 코드` 및 confirmation.html 본문 저장 성공 확인.
- Reset password 제목 `Campus Commons 비밀번호 재설정 코드` 및 recovery.html 본문 저장 성공 확인.
- 두 메일 모두 `{{ .Token }}` 코드 방식이며 한국어 미리보기 확인. 링크/딥링크를 통한 가입 확인을 요구하지 않음.
- 사용자가 지정한 수신 주소로 실제 `/auth/v1/signup` 요청 HTTP200, 확인 전 access token 미발급 확인. 임시 검사 비밀번호는 검사 프로세스 메모리에만 유지하며 사용자 주소/비밀번호/코드/토큰은 문서·PR에 기록하지 않음.

## 실제 Auth·DB 연결 검증
- 사용자가 가입 메일 수신 후 코드를 제공했고 `/auth/v1/verify` HTTP200·email_confirmed_at 존재 확인.
- 실제 이메일+임시 비밀번호 로그인 HTTP200. 발급 JWT 헤더 ES256 확인.
- 실제 사용자 토큰으로 account read/enter/read 모두 HTTP200. 최초 UUID 귀속·XP0·레벨1 검증 통과.
- 임시 검사 도구가 DB 열 이름 last_nickname으로 API 응답을 검사해 닉네임 두 항목을 false로 표시한 것을 조사. 실제 API 계약은 nickname이며 Unity DTO도 nickname을 사용함. 별도 읽기 전용 SQL로 저장된 닉네임 `연결테스트`, XP0·레벨1, account_read의 nickname과 DB 값 일치=true 확인. 제품 코드 변경 없음.
- 인증된 클라이언트의 player_progress 직접 수정 HTTP403/42501, 비활성 퀴즈 보상 요청 HTTP409/reward_source_not_enabled 확인.
- refresh HTTP200, 해당 세션 logout HTTP204, 로그아웃한 refresh token 재사용 HTTP400 확인.
- recovery 메일 요청 HTTP200 및 사용자 수신 확인. 테스트 비밀번호·토큰을 보관하던 검사 프로세스는 종료함.

## 남은 사용자·실환경 검증
- 테스트 계정을 실제로 쓰려면 Unity의 비밀번호 찾기에서 사용자가 원하는 비밀번호를 직접 설정해야 함. 새 비밀번호 입력·확인·제출은 사용자 작업이며 채팅이나 저장소로 받지 않음.
- 두 계정 분리·다른 PC, 지정 Unity6000.4.5f1의 실제 Auth 연결 및 standalone 검증. 임시6000.6의 기존 Unity 검사는 fixture이므로 위 실제 HTTP/DB 검증과 구분함.
- 자동 레벨 상승·경험치·재화·외형 보상은 다음 작업. 현재 progression은 설정과 무관하게 비활성.
