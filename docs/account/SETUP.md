# Supabase 계정·DB 설정

이 브랜치 `codex/cc-8-account-progression`의 계정 모듈은 메인 씬에서 자동으로 시작합니다. 이메일+비밀번호 → 이메일 코드 확인 → 입장 닉네임 → 로비 순서이며, 로그아웃하면 게임 입력과 계정 캐시를 지웁니다. 초기 경험치는 0, 레벨은 1입니다. 토큰/비밀번호는 디스크나 PlayerPrefs에 저장하지 않습니다.

현재 연결된 Supabase 프로젝트가 없어 실제 서비스 생성/배포·외부 메일 수신은 수행하지 않았습니다. 아래 1~6을 운영자가 설정하면 실서비스 테스트를 할 수 있습니다. DB 스키마는 `supabase/migrations/20261109081917_account_progression.sql`, 메일 본문은 `supabase/templates/`, 함수는 `supabase/functions/`에 있습니다.

## 1. 무료 프로젝트 만들기

[Supabase Dashboard](https://supabase.com/dashboard)에서 로그인 → New project → 조직과 Free 요금제 선택 → 프로젝트 이름(예: campuscommons) → 강한 DB 비밀번호 설정 → 사용자와 가까운 지역 선택 → 생성 완료까지 기다립니다. DB 비밀번호는 비밀번호 관리자에 보관하고 채팅이나 Git에 올리지 않습니다.

프로젝트 Settings에서 Project ID(ref), Project URL, API Keys의 **Publishable key (`sb_publishable_...`)** 위치를 확인합니다. URL은 보통 `https://<ref>.supabase.co`입니다. Unity에는 URL과 publishable key만 입력합니다. secret key/service_role/DB 비밀번호는 Unity에 넣지 않습니다. 기존 프로젝트에 적용한다면 아래 테이블/함수 이름 충돌과 기존 데이터를 먼저 확인합니다.

## 2. 이메일 인증 설정

Authentication 설정에서 다음을 적용합니다. Dashboard 메뉴 이름은 버전에 따라 Email provider / Sign In & Providers / Email 등으로 표시될 수 있습니다.

- Email provider와 신규 회원가입 활성화
- Confirm email 활성화, Anonymous sign-in 비활성화
- 최소 비밀번호 길이 **12자**. 클라이언트는 12~128자로 검사하고 최종 허용 여부는 Auth가 판단합니다.
- 이메일 OTP 길이 **6자리**, 만료 **600초**, 재전송 간격 최소 **60초** 권장
- Recovery 검증 직후 새 비밀번호를 설정하는 흐름입니다. 추가 재인증 nonce를 강제하는 별도 정책은 현재 클라이언트 범위에 포함하지 않았습니다.

`supabase/config.toml`의 Auth 값은 **로컬 스택 설정**입니다. `link`나 DB migration을 실행했다고 hosted Dashboard의 Auth/SMTP 설정까지 자동 반영되는 것은 아닙니다.

Authentication → Email Templates에서 다음 본문을 각각 붙여 넣습니다.

| 메일 | 사용할 파일 |
|---|---|
| Confirm signup | `supabase/templates/confirmation.html` |
| Reset password / Recovery | `supabase/templates/recovery.html` |

본문의 `{{ .Token }}`을 유지해야 합니다. 이 구현은 이메일 안의 링크 대신 **게임에서 코드를 입력**하므로 앱 딥링크나 별도 인증 웹페이지를 요구하지 않습니다. Site URL에 비밀값을 넣지 마세요.

## 3. SMTP 설정

일반 사용자가 메일을 받으려면 Authentication → SMTP Settings에 별도 SMTP 발송 서비스를 연결합니다. 제공자의 호스트·포트·SMTP 사용자명·비밀번호와 인증된 발신 주소/이름을 입력합니다. 발신 도메인 인증(SPF/DKIM 등)이 필요하면 제공자 안내대로 DNS에 등록합니다. SMTP 비밀번호는 Supabase 서버 설정에만 보관합니다.

Supabase 기본 SMTP는 프로젝트 조직의 팀원 주소에만 발송하며 현재 시간당 2통 제한이 있습니다. 외부 학생들을 메일 수신 목적으로 조직 관리자에 초대하지 마세요. 조직에 이미 등록된 테스트 계정으로만 제한적으로 시험하거나 정상 SMTP를 설정합니다. 메일 확인을 끄고 전체 기능 검증이 끝났다고 간주하지 않습니다.

DB/Auth 무료 한도와 SMTP/발신 도메인 비용은 별개입니다. 무료 SMTP 한도를 사용할 수 있어도 도메인 구매까지 포함해 비용이 항상 0원이라고 보장하지 않습니다. [공식 SMTP 안내](https://supabase.com/docs/guides/auth/auth-smtp)

## 4. DB와 함수 배포

터미널에서 **이 worktree의 프로젝트 루트**로 이동합니다. CLI **3.0.0**의 실제 `--help`를 기준으로 확인한 명령입니다. CLI 3에서는 DB push가 기본적으로 로컬을 대상으로 하므로 **`--remote`를 명시**합니다. 다른 CLI 버전은 해당 버전의 help를 확인하세요.

```sh
npx --yes supabase@3.0.0 login
npx --yes supabase@3.0.0 link --project-ref YOUR_PROJECT_REF
npx --yes supabase@3.0.0 db push --remote --dry-run
npx --yes supabase@3.0.0 db push --remote
npx --yes supabase@3.0.0 functions deploy account progression --use-api
```

`YOUR_PROJECT_REF`를 자신의 ID로 바꿉니다. 로그인은 브라우저에서 하고, CLI가 DB 비밀번호를 요구하면 로컬 터미널에 입력합니다. `--use-api`는 함수 번들링에 로컬 Docker가 필요 없게 합니다. `--no-verify-jwt`나 `--prune`은 붙이지 않습니다.

migration은 profiles/player_progress/xp_events 및 private 내부 함수·활동 테이블을 생성하고 기존 Auth 계정도 초기화합니다. Auth 사용자를 만들면 프로필과 XP0/레벨1 행이 같은 트랜잭션에서 생성됩니다. SQL Editor에 같은 migration을 따로 실행하고 다시 CLI로 중복 적용하지 마세요. 이 문서는 CLI 경로를 기준으로 합니다.

Integrations → Data API에서 Data API가 켜져 있고 **public** schema가 노출되는지 확인합니다. **private schema는 노출하지 않습니다.** client는 DB의 성장 행을 수정할 권한이 없습니다. 서버 전용 public RPC wrapper에도 anon/authenticated EXECUTE 권한이 없습니다.

함수는 기본 환경변수 `SUPABASE_URL`과 `SUPABASE_PUBLISHABLE_KEYS`/`SUPABASE_SECRET_KEYS`의 default key를 우선 사용합니다. 이전 환경에서는 `SUPABASE_ANON_KEY`/`SUPABASE_SERVICE_ROLE_KEY`로 호환됩니다. 이 값들은 Supabase가 서버 환경에 제공합니다. 클라이언트로 복사하지 않습니다. 두 함수의 Verify JWT 설정을 활성 상태로 유지합니다. 함수 자체에서도 Auth `/user`로 토큰·이메일 확인 상태·익명 계정 여부를 검증합니다. [공식 환경변수 안내](https://supabase.com/docs/guides/functions/secrets)

**`ACCOUNT_REWARD_SOURCES`는 처음에는 설정하지 않습니다.** 현재 로컬 게임 결과를 검증 없이 경험치로 바꾸지 않기 위해 실제 보상은 기본 비활성입니다. 서버가 완료를 검증한 activity_runs만 지급할 수 있으며, 기능별 계약을 완료한 다음 해당 source만 활성화합니다.

Dashboard의 Security/Performance Advisor에서 경고를 확인합니다. migration은 RLS·고정 search_path·명시적 GRANT/REVOKE를 포함하지만 hosted Advisor는 실제 프로젝트에서 별도로 실행해야 합니다.

## 5. Unity에 공개 연결값 입력

1. Unity Hub에 `CampusCommons-account-progression` 폴더를 프로젝트로 추가합니다.
2. 프로젝트 지정 버전 **6000.4.5f1**로 엽니다.
3. 메뉴 **Campus → 계정 → Supabase 연결 설정**에서 Project URL과 Publishable key를 입력하고 저장합니다.
4. `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity`를 열고 Play합니다.

공개 연결값은 `Assets/Account/Resources/AccountConnection.json`에 저장되어 빌드에도 들어갑니다. 비밀이 아니므로 클라이언트에 포함될 수 있으며, 안전성은 key를 숨기는 대신 Auth/RLS/서버 권한으로 지킵니다. 기본 파일은 빈 값이어서 설정 전에는 입장을 허용하지 않습니다. HTTP 주소나 secret/service_role key는 설정 창에서 거부합니다. Play 중 연결값을 바꿨다면 Play를 재시작합니다.

## 6. 실제 사용자 확인

- 회원가입에서 이메일/비밀번호/확인을 입력 → 메일의 6자리 코드 확인 → 닉네임 입력 → 로비 입장
- 앱 종료 후 같은 계정으로 다시 로그인 → 닉네임을 바꿔도 동일한 계정의 XP·레벨이 표시되는지 확인
- 서로 다른 두 계정과 다른 PC에서 데이터가 섞이지 않는지 확인
- 로그아웃하면 즉시 이동·포털 입력이 막히고 로그인 화면으로 돌아오는지 확인
- 비밀번호 찾기 → 메일 코드 → 새 비밀번호 → 다시 로그인
- 잘못된 코드/만료 코드/중복 가입/재전송 제한/네트워크 단절에서 오류 안내와 재시도를 확인
- Dashboard의 Authentication Users와 profiles/player_progress를 대조. **사용자마다 XP0/레벨1로 시작하는 것은 정상**이며 보상 연동을 완료하기 전에는 자동으로 경험치가 오르지 않습니다.

전송 실패 시 저장 성공으로 표시하지 않습니다. 보상 API를 사용할 때는 동일한 활동 ID로 재시도합니다. 매 프레임이나 종료할 때 전체 XP를 덮어쓰는 방식은 사용하지 않습니다.

## 7. 개발 검사와 운영

```sh
# 아래 DB 검사는 실제 회원 DB가 아닌 일회용 로컬 DB에서만 실행
psql "$ACCOUNT_TEST_DATABASE_URL" -v ON_ERROR_STOP=1 -f Tools/account/bootstrap_test_db.sql
psql "$ACCOUNT_TEST_DATABASE_URL" -v ON_ERROR_STOP=1 -f supabase/migrations/20261109081917_account_progression.sql
python3 Tools/account/run_database_tests.py
# Edge 함수: 외부 요청은 fixture로 대체하며 실메일 검증과 구분
 deno test supabase/functions/tests/
```

`bootstrap_test_db.sql`은 기본 PostgreSQL에서 Supabase 역할·auth.uid()를 흉내 내는 **검사용 schema**입니다. hosted Supabase나 이미 초기화된 Supabase 로컬 스택에 실행하지 않습니다. `ACCOUNT_TEST_DATABASE_URL`은 개발자 로컬 환경에 설정합니다. DB 검사는 RLS·미확인 계정·중복/동시 보상·중간 실패 롤백을 실제 PostgreSQL에서 실행합니다.

C# 세션 검사는 `Tools/account/SessionTests.cs`와 AccountModels.cs/AccountSession.cs를 함께 컴파일해 실행합니다. Unity 메인 씬 검사는 `-executeMethod CampusCommons.Account.Editor.AccountFlowTests.Run`입니다. Auth fixture를 사용하므로 실제 Supabase/SMTP 성공을 뜻하지 않습니다.

Free 요금제는 DB500MB·월활성5만·egress5GB 및 비활성 프로젝트 pause 조건이 있습니다. 이벤트 이력 증가량을 확인하고, 발표 전 Dashboard에서 휴면 해제와 실로그인을 점검합니다. 주기적인 암호화 DB export를 Git 밖의 운영자 저장소에 보관하고 일회용 프로젝트에서 복구를 연습합니다. Auth 사용자 정보도 개인정보이므로 export를 저장소/이슈에 올리지 않습니다. 백업 명령은 사용 중 CLI `db dump --help`와 [공식 백업 안내](https://supabase.com/docs/guides/platform/backups)에 따릅니다.

검증 환경·통과/미검증 내역은 `docs/tasks/cc-8.md`에 기록합니다. 원본 Unity 버전과 다르거나 fixture 응답을 사용한 검사는 실제 운영 검증과 구분합니다.
