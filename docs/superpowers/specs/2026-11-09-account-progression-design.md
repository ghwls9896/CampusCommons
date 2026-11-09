# 계정·개인 성장 데이터 설계 — CC-8

상태: 사용자 요구와 서비스 선택 확인 완료, 세부 정책은 아래 제안. 구현 전 검토용.
날짜: 2026-11-09 / 담당 HIGHCHOI / Issue #8
기준: origin/main fc5d8c7 / 브랜치 codex/cc-8-account-progression

## 확정 요구와 제안 구분
확정: 이메일+비밀번호 회원가입/로그인, 입장할 때 닉네임 입력, 사용자별 경험치·레벨 영구 저장, Supabase Auth+PostgreSQL, 무료 요금제 우선.
제안: 매 입장 닉네임 재확인(직전 값 미리 채움), 닉네임 2~12자/중복 허용, 기본 레벨1/XP0, 자동 로그인 없는 메모리 세션, 아래 성장 곡선. 보상 원천과 지급량은 각 기능과 계약한 다음 활성화한다.

## 접근 비교
- 선택: Supabase Auth+PostgreSQL. 인증을 직접 만들지 않고 SQL 트랜잭션·유일 제약·RLS로 성장 데이터 일관성을 관리한다.
- 대안: Firebase Auth+Firestore. Unity SDK와 인증 메일 운영이 편리하나 문서 DB 모델과 보상 서버의 설계가 달라진다.
- 자체 FastAPI 인증+외부 PostgreSQL: 기존 퀴즈 서버와 언어는 맞지만 비밀번호/복구/세션 운영 책임과 PR #4 접점이 늘어난다.
Render 무료 서버의 SQLite 파일이나 무료 Render Postgres는 영구 계정 DB로 선택하지 않는다. 전자는 재시작 시 소실될 수 있고 후자는 생성 30일 후 만료된다.

## 무료 운영 범위와 선행 설정
2026-11-09 공식 문서 확인: Supabase Free $0/월, DB500MB, 월 활성 사용자50,000, egress5GB, 활성 프로젝트2개, 1주 비활성 시 pause. 수업·소규모 시연의 초기 용량으로 제안하며 무제한/무중단을 보장하지 않는다.
기본 SMTP는 프로젝트 팀에 등록된 주소에만 발송하며 현재 시간당2통 제한. 일반 사용자 가입확인·비밀번호복구에는 custom SMTP가 필요하다. SMTP 무료 한도, 발신 도메인 보유/인증 여부와 추가 비용을 실제 제공자 선택 시 확인한다. 도메인 구매까지 포함한 완전 무료 운영은 아직 확정하지 않았다.
가입확인/복구는 Unity에서 코드를 입력하는 OTP 방식으로 계획해 별도 웹사이트나 커스텀 URL 스킴 의존을 줄인다. 이메일 확인을 끄는 편법을 실제 회원가입 완료로 취급하지 않는다. 발신 설정 미완료 시 로컬 Mailpit/등록된 테스트 주소로만 검증한다.
Free는 직접 암호화된 정기 DB export 및 복구 리허설 계획을 포함한다. 사용자 DB/덤프는 Git에 넣지 않는다. 장기 보관은 별도 운영자 저장소에서 관리한다.

## 사용자 흐름
실행 → 로그인/회원가입 → 필요 시 이메일 코드 확인 → 계정 세션 성립 → 내 프로필·성장 데이터 조회 → 닉네임 입력 → 서버 저장 확인 → 로비 입장.
가입은 이메일·비밀번호·비밀번호 확인. 비밀번호는 12~128자 제안, 서버 Auth 설정과 UI 검증을 맞춘다. 이메일은 trim하고 인증 서비스 규칙을 따르며 임의 주소 변형은 하지 않는다.
매 입장 닉네임 입력 창에 직전 닉네임을 채워 준다. NFC 정규화/앞뒤 공백 제거 후 Unicode 코드포인트2~12개, 제어문자/줄바꿈/리치텍스트 태그 제외. 중복은 허용하고 필요하면 공개용 짧은 식별 태그를 붙인다. 닉네임은 로그인 키가 아니다.
초기 버전은 비밀번호/토큰을 PlayerPrefs에 저장하지 않는다. 세션은 메모리에 두고 앱 종료 시 로그인 화면부터 시작한다. 실행 중 access token 만료는 refresh token으로 갱신한다.
로그아웃/다른 계정 로그인 시 요청 취소, 캐시/HUD/닉네임/XP 초기화. 이전 계정의 늦은 응답은 세션 세대 번호로 무시한다. 로그인·조회·저장 실패 시 성공처럼 입장시키지 않고 재시도한다. 인증 토큰 만료가 복구되지 않으면 로그인 화면으로 이동한다.
비밀번호 찾기는 이메일 코드 확인 후 새 비밀번호 설정, 가입확인 재전송과 요청 제한/일반화된 오류 메시지를 포함한다.

## 구조와 데이터
Unity Assets/Account/ → Supabase Auth REST(이메일/비밀번호·OTP·세션) 및 Data API(자기 데이터 읽기).
닉네임 저장과 보상 검증은 Supabase Edge Functions의 account/progression 모듈로 분리한다. 게임 클라이언트에 DB비밀번호·service_role/secret key를 넣지 않는다. 공개 publishable key와 사용자 access token만 사용한다.
외부 인증/DB/Edge Functions 프로젝트의 생성·배포는 이 계획에서 실행하지 않는다.

| 저장 대상 | 주요 필드 | 원칙 |
|---|---|---|
| auth.users (서비스 관리) | id UUID, email, 인증 자격정보 | 앱 자체 테이블에 비밀번호를 복제하지 않음 |
| profiles | user_id PK/FK, last_nickname, created_at, updated_at | 이메일을 공개 프로필에 복제하지 않음 |
| player_progress | user_id PK/FK, total_xp bigint>=0, level, updated_at | 계정 생성 시 XP0/레벨1 |
| xp_events | id, user_id, source, source_event_id, delta, policy_version, created_at | append-only, UNIQUE(user_id,source,source_event_id) |
| activity_runs (보상 원천 연동 단계) | id, user_id, source, server-issued state, result, finalized_at | 서버가 확인할 수 있는 실행 결과와 소유권 |

프로필·성장 행 초기화는 auth.users 신규 생성 시 최소 트리거로 원자적으로 생성한다. 인증 완료 전 게임 데이터 접근은 거부한다. 트리거 실패/기존 누락 행은 무작정 재가입시키지 않고 운영 복구·멱등 초기화 경로로 처리한다.
level은 total_xp에서 DB의 동일한 immutable 규칙으로 계산하는 generated stored column으로 제안한다. 독립적인 XP/레벨 덮어쓰기를 허용하지 않는다. 규칙 변경은 버전 관리된 migration과 기존 행 재계산으로 수행한다.
레벨 L 최소 누적 XP = 50*(L-1)*L. 예: 레벨1=0, 레벨2=100, 레벨3=300, 레벨4=600. 이는 제안이며 보상 경제 확정이 아니다.
테이블은 RLS와 최소 GRANT를 함께 적용해 authenticated라도 auth.uid()=user_id인 자기 행만 읽는다. anon 접근, 다른 사용자 행 조회, client의 XP/event INSERT·UPDATE·DELETE는 거부한다. 프로필 수정도 검증 함수 경유. public view/RPC가 이 규칙을 우회하지 않게 실제 역할로 테스트한다.

## 경험치의 신뢰와 일관성
클라이언트는 xp=100 같은 증가량이나 대상 user_id를 지정하지 않는다. 검증된 JWT의 sub로 사용자를 결정하고, 서버가 발급·완료 검증한 활동 ID만 제출한다. 서버가 정책에서 지급량을 계산한다.
신뢰된 보상 함수는 원천 활동 소유권/완료 상태/재사용/상한을 확인한 뒤 단일 DB 트랜잭션으로 이벤트 삽입과 total_xp의 원자적 증가를 수행한다. 동일 event 재전송은 이미 처리된 결과를 반환한다. 중간 실패는 모두 롤백한다. 동일 계정 동시 완료는 행 잠금/원자 UPDATE로 누락을 막는다.
privileged 보상 함수는 외부 노출 schema의 무제한 SECURITY DEFINER RPC로 만들지 않는다. 내부 함수 권한은 서버 전용 역할에 한정하고 역할별 호출 테스트를 한다. Edge endpoint 자체도 사용자 JWT와 활동 소유권을 검증한다.
응답: {total_xp,level,next_level_total_xp,awarded_xp,already_awarded}. 종료 시 전체 스냅샷 덮어쓰기 대신 보상 확정 시 즉시 저장한다. 전송 결과 불명확 시 같은 event로 재시도하며, 오프라인 상태의 로컬 보상을 임의 확정하지 않는다.
중복 재시도에는 해당 이벤트의 awarded_xp=0, already_awarded=true와 최신 성장 데이터를 반환한다.
중요한 한계: 현재 퀴즈 정답·탈출 판정과 웹캠 결과는 클라이언트에 있다. JWT/RLS/서버 지급량만으로 결과 위조가 막히지 않는다. 초기 계정 단계에서는 XP0 저장·조회와 테스트 보상으로 검증한다. 실제 보상은 서버가 소유·검증하는 활동 결과가 준비된 기능만 활성화한다. 웹캠 로컬 추론은 검증된 학습 증명으로 간주하지 않는다.

## 코드 경계와 통합 순서
이 PR은 설계 문서/작업 명세만 제출한다. 향후 Assets/Account/, supabase/, Tools/account/를 먼저 독립 구현하고 실제 수정 경로를 task scope에 추가한다.
Unity는 기존 UnityWebRequest/JsonUtility 패턴을 우선한다. 필요한 패키지는 별도 선언한다. Unity 버전6000.4.5f1 유지.
현재 GameStart.Awake는 바로 Player를 생성한다. 테스트용 계정 화면에서 먼저 검증하고, 최종 통합에서 GameStart가 인증/프로필 준비 후 Player를 활성화하도록 작은 변경을 한다. 씬에 이미 Player가 있는 경우도 이동/카메라/포털/HUD가 인증 전 작동하지 않아야 한다. 다른 MonoBehaviour의 입력도 통합 검증한다.
PR #4도 GameStart/Player/Packages를 수정하므로 별도 통합 Issue/PR에서 최신 main과 담당자 확인 후 연결한다. Issue #7 웹캠 코드를 이 브랜치에 합치지 않는다. 이 task 선언은 공유 파일 잠금이나 변경 승인 의미가 아니다.

## 완료 기준과 단계
1. DB migration·RLS·초기화·성장 계산·멱등 트랜잭션을 로컬 실제 PostgreSQL 역할로 검증.
2. 이메일 가입·로그인·확인·복구·갱신·로그아웃과 계정 전환을 테스트용 사용자 두 명으로 검증.
3. 닉네임 저장·내 성장 조회·입장 화면·HUD를 독립 테스트 씬에서 구현.
4. 지정 Unity6000.4.5f1에서 메인 씬 연결, 앱 종료/다른 PC 로그인 복원, 실패/재시도·데이터 격리 확인.
5. 보상 원천별 서버 검증 계약 확정 후 각각 별도 연동. 현 시점 XP 지급량/퀴즈 신뢰 경로는 미확정이므로 본 계정 구현을 막지 않되 실제 보상 활성화는 막는다.
최종 리뷰/통합 담당 ghwls9896. CI 통과와 실제 이메일·DB·Unity 동작 검증을 구분한다.

## 근거
- https://supabase.com/pricing
- https://supabase.com/docs/guides/auth/passwords
- https://supabase.com/docs/guides/auth/auth-smtp
- https://supabase.com/docs/guides/database/postgres/row-level-security
- https://supabase.com/docs/guides/platform/backups
- https://firebase.google.com/pricing
- https://render.com/docs/free
