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
- 연결된 Supabase 프로젝트 없음. hosted DB/함수 배포·실제 메일·외부 PC 로그인은 사용자 설정 후 확인 필요.
- 설정 순서: docs/account/SETUP.md. CLI3.0은 db push --remote 명시. Auth/SMTP Dashboard 설정은 local config가 자동 반영하지 않음.
- 기본 SMTP는 외부 학생에게 발송할 수 없음. 일반 사용자 가입확인/복구에는 custom SMTP와 발신 인증 필요.
- Docker 미실행: 전체 Supabase 로컬 Auth 스택 대신 실제 PostgreSQL14+역할 helper로 DB 검사. hosted PG/Advisor는 별도 확인.
- Unity6000.4.5f1은 설치돼 있지 않아 원본 버전 실행/standalone 빌드는 미검증. 원본 ProjectVersion은 유지.
- 실제 퀴즈/탈출/웹캠 보상은 아직 비활성. 서버가 결과를 검증하는 원천별 계약 후 ACCOUNT_REWARD_SOURCES에 해당 source만 활성화. 클라이언트 성공 플래그로 지급하지 않음.
- 운영 백업 export·복구 리허설은 실제 프로젝트 생성 후 운영자가 수행.

다음 행동: 최종 코드 검토/CI → Supabase 무료 프로젝트·Auth/메일·migration/functions 설정 → 실제 이메일 두 계정·지정 Unity 버전 확인 → 사람 리뷰/통합.
