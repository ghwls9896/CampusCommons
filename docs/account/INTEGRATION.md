# 계정과 독립 레벨 연동 계약

`AccountGate.Instance`는 실행 시 생성되고 SceneLoaded에서 기존/생성된 Player의 이동·컨트롤러·QuizRoom 입력을 로그인 완료 전 차단합니다. 기존 GameStart/Player/씬 파일을 수정하지 않습니다. 새 캐릭터/방 모듈 도입 시 AccountWorldGate 차단 대상도 검증합니다. 클라이언트 입장 차단과 별도로 서버는 모든 계정 요청을 인증합니다.

- `EntryReady(AccountProfile)`: 이메일 인증·닉네임 서버 저장 완료 후 발생
- `EntryRevoked`: 로그아웃 또는 인증 갱신 거부 시 로컬 퀴즈/탈출 게임을 종료하고 입력 차단
- `ReadProfileAsync()`: 로그인한 본인의 UUID·마지막 닉네임·레벨 조회
- `EnterAsync(nickname)`: 본인 닉네임을 저장하고 같은 계정의 레벨 반환
- 신규 회원은 Lv.1, DB와 Unity 모두 유효 범위는 Lv.1~15
- 다른 계정 공개 프로필/랭킹, 자동 레벨 상승, 재화, 5·10·15레벨 외형 보상은 이번 범위에 없음

## API

```text
POST /functions/v1/account  Authorization: Bearer <user access token>
{"action":"read"}
또는 {"action":"enter","nickname":"학생"}
→ {"user_id":"<본인 UUID>","nickname":"학생","level":1}

POST /functions/v1/progression  Authorization: Bearer <user access token>
→ HTTP 409 {"error":{"code":"progression_not_enabled"}}
```

UUID는 함수가 Supabase Auth에서 확인한 사용자 ID만 사용합니다. 클라이언트가 user_id/level/XP를 보내거나 레벨 수정 action을 호출하면 거절됩니다. 미인증/미확인/익명 계정은 401이며 public RPC는 service_role만 실행할 수 있습니다. 각 회원은 RLS를 통해 본인 행만 읽을 수 있고 레벨 직접 수정 권한은 없습니다.

`player_progress.level`은 NOT NULL 정수, DEFAULT 1, CHECK 1..15입니다. 기존 total_xp/xp_events/private.activity_runs는 데이터 보존 목적으로 남아 있지만 레벨 계산 및 현재 UI/API에는 사용하지 않습니다. public.account_award/private.award_activity 모두 SQLSTATE 55000으로 지급을 차단합니다. ACCOUNT_REWARD_SOURCES 설정으로 활성화할 수 없습니다.

## 관리자 수동 레벨 설정

현재 자동 상승이 없으므로 테스트나 운영상 필요한 레벨 변경은 신뢰된 관리자가 Supabase SQL Editor에서 특정 UUID 한 행을 대상으로 합니다. 사용자 입력이나 클라이언트 API에 이 권한을 노출하지 않습니다.

```sql
-- 실제 대상 UUID로 교체하고 변경 전 해당 회원을 확인하세요.
UPDATE public.player_progress
SET level = 5, updated_at = now()
WHERE user_id = '00000000-0000-0000-0000-000000000000'::uuid
RETURNING user_id, level;
```

다음 로그인 또는 `ReadProfileAsync()`에서 최신 값이 보입니다. 닉네임 변경/입장으로 레벨이 초기화되지 않습니다. 0·16·NULL은 DB에서 거절됩니다.

## 적용 순서와 호환성

초기 migration → standalone_account_level migration → account/progression 함수 → 이 계약을 사용하는 Unity 클라이언트 순서입니다. 이전 XP 응답을 필수로 검사하는 클라이언트는 새 서버 응답을 거절하므로 함께 업데이트해야 합니다. 아직 배포되지 않은 개발 클라이언트 기준 변경입니다. 기존 계정·레벨·XP 이력은 유지하며, 기존 레벨이 1~15 밖이면 migration 전체가 실패하고 관리자 판단을 기다립니다. 레벨을 임의로 낮추지 않습니다.
