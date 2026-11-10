# campuscommons 실제 배포 현황 — 2026-11-10

프로젝트 ref: gumkrzpnzaaxqlpswwvn / API URL: https://gumkrzpnzaaxqlpswwvn.supabase.co
사용자가 만든 Free 프로젝트에 Chrome Dashboard에서 설정. Supabase MCP 연결에는 이 프로젝트 권한이 없어서 로그인된 UI로 수행.

## 완료·확인
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

## 남은 사용자 입력
현재 Auth → SMTP 화면에 Sender name=Campus Commons, Host=smtp.gmail.com, Port587까지 입력했으며 아직 저장하지 않음.
사용자가 Sender email address와 Username에 발신 Gmail을 넣고, Password에 Google 앱 비밀번호를 직접 입력한 뒤 Save changes를 눌러야 함. 계정 자격정보 입력·확인·저장은 브라우저 제어 보안 규칙상 사용자에게 인계.
기본 무료 SMTP에서는 커스텀 메일 템플릿 편집이 잠겨 있음. SMTP 연결 후 confirmation/recovery.html의 코드 템플릿 적용 → 실제 이메일 회원가입/복구·Unity 로그인 검증을 계속한다.
새 회원을 아직 만들거나 사용자 이메일로 테스트 메일을 발송하지 않았음. 실제 로그인 세션으로 읽기/입장·새 JWT 호환성 최종 확인은 SMTP 이후 필요.
