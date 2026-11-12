# Standalone Account Level Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 이메일 계정의 레벨1~15를 경험치와 독립적으로 저장·조회한다.
**Architecture:** 기존 level 열을 보존 전환하고 계정 응답·Unity 모델/화면을 함께 바꾼다. XP 지급은 서버·DB 양쪽에서 차단한다.
**Tech Stack:** PostgreSQL, Supabase Auth/Edge Deno, Unity C#.
**Spec:** docs/account/LEVEL_ONLY_DESIGN.md

## Global Constraints
- 기존 데이터 보존, 레벨 기본1/범위1~15, 경험치·재화·아이템 지급 연기.
- 클라이언트 레벨 수정 금지. 기존 독립 worktree/PR 사용. 공유 씬·Unity6000.4.5 설정 유지.
- 원본 migration은 변경하지 않는다. 공개 계정 API 응답은 {user_id,nickname,level}.

## Review Focus
- 상한 밖 기존 데이터 때문에 migration이 실패할 때 값과 원래 schema가 보존되는가 (Task1).
- 환경변수로 보상이 켜져 있어도 저장 XP·레벨이 바뀌지 않는가 (Task1).
- 레벨15/누락·0·16 응답을 올바르게 처리하는가 (Task2).
- 복구 실패/계정 전환의 지연 응답이 이전 계정으로 입장시키지 않는가 (Task2).
- 실제 Unity의 비동기 프로필/화면 전환·로그아웃에서 레벨 표시가 최신 계정과 일치하는가 (Task2).

### Task 1: 데이터 보존 migration과 서버 권한
**Files:** supabase/migrations/20261112013634_standalone_account_level.sql, supabase/functions/_shared/handler.ts, supabase/functions/tests/account_test.ts, supabase/tests/database/account_progression.test.sql, Tools/account/run_database_tests.py
**Interfaces:** consumes 기존 auth.users/profiles/player_progress 및 account RPC. Produces level1~15와 {user_id,nickname,level}; progression은 authenticated 요청에409 progression_not_enabled; DB 보상은55000.
- [x] 기존 테스트를 새 계약으로 수정. 기본1, 관리자5/10/15 저장,0/16/null 거절, XP 변경과 레벨 독립, 타인 읽기/클라이언트 수정 거절, 닉네임 변경 시 레벨 보존, 보상 비활성 및 migration 기존 값 보존 검사.
- [x] 초기 schema에서 검사 실패를 확인. Expected: generated level에 직접 UPDATE 불가/XP 필드 노출/활성 source의 보상 허용.
- [x] DROP EXPRESSION으로 데이터 보존 전환, default/check/not-null 설정, progress_json 갱신, public/private 보상 함수55000, Edge progression 차단.
- [x] Run: ACCOUNT_TEST_DATABASE_URL을 일회용 DB로 지정한 python3 Tools/account/run_database_tests.py 및 deno test supabase/functions/tests/. Expected: ACCOUNT_DATABASE_PASS 및 모든 Deno 테스트 통과. migration 전후 스냅샷 검사 포함.
- [x] 검증된 변경을 명시적으로 커밋.

### Task 2: Unity 계정 흐름과 배포 마무리
**Files:** Assets/Account/Scripts/AccountModels.cs, AccountSession.cs, AccountView.cs, Assets/Account/Editor/AccountFlowTests.cs, Tools/account/SessionTests.cs, docs/account/, docs/tasks/cc-8.md
**Interfaces:** consumes Task1의 {user_id,nickname,level}. Produces XP 없는 닉네임/레벨 표시와 기존 인증 흐름.
- [x] C# 검사에 XP 없는 레벨1/5/10/15 응답, 범위 밖 거절, 재로그인/닉네임 변경 보존, 잘못된 OTP·미확인 가입/복구 실패·복구 중 입장 거절을 추가하고 실행. Expected: 기존 XP 의존 검사 실패.
- [x] DTO·세션 유효성·UI를 level만 사용하도록 수정. 기존 Auth 흐름 유지, 확인 코드는 설정과 같은6자리 검증.
- [x] Mono로 SessionTests를 컴파일/실행. Expected: ACCOUNT_SESSION_PASS. 임시 Unity 실제 씬 fixture를 Lv15로 검사. Expected: ACCOUNT_FLOW_PASS, 원본 버전 불변.
- [x] 로컬 전체 검사 완료 후 hosted 현재 값 확인 → migration 이력과 함께 트랜잭션 적용 → 두 함수 배포 → 서버 보호/레벨 응답 확인. 사용자의 비밀번호를 기록하지 않는다.
- [ ] 문서·PR/Issue 갱신, 커밋 후 python3 Tools/team/check.py --base origin/main. Expected: TEAM_CHECK_PASS. 전체 변경 독립 검토 후 중요 수정 검증, 기존 브랜치 push.
