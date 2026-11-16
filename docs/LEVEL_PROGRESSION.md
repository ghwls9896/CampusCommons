# CampusCommons — Lv.1~15 / XP / 외형 보상 정책 초안

관련 작업: [Issue #32](https://github.com/ghwls9896/CampusCommons/issues/32). 담당: GongBeak.

## 현재 상태와 작업 경계

- 계정별 레벨은 이미 Supabase `player_progress.level`에 독립 정수(1..15)로 저장된다.
- `player_progress.total_xp`와 `xp_events` 이력은 보존되어 있지만 자동 지급 기능은 **비활성**이다.
- `/functions/v1/progression`은 409 `progression_not_enabled`를 반환하고, DB 보상 함수는 SQLSTATE 55000을 발생시킨다.
- 이 브랜치의 `Assets/TeamProgression/LevelProgressionRules.cs`는 **순수 정책 함수**만 제공한다. `AccountLevelUnlocks.cs`는 서버 확인이 끝난 계정 프로필의 레벨을 읽어 외형 해금 플래그만 제공한다. 레벨 저장, 서버 지급, Sunbox 모델, 공통 씬은 수정하지 않는다.
- 실제 XP 발급 및 DB migration/API 계약 변경은 계정 담당 HIGHCHOI, 탈출 결과는 ghwls9896와 조율한 후 별도 변경으로 진행한다.

## 제안한 경험치 정책

| 결과 | XP |
| --- | ---: |
| 퀴즈 승리 | +100 |
| 퀴즈 패배 | +30 |
| 퀴즈 무승부 | +50 |
| 탈출 성공 | +100 |
| 탈출 실패 | +30 |

공부 10분당 +20 XP는 **후속 검토안**이며 이번 정책 코드에 포함하지 않는다. 공부 기록은 30초마다 재전송될 수 있으므로 서버가 확인한 유효 공부 구간을 기준으로 중복 지급을 막아야 한다.

## 레벨 기준

Lv.N 도달 누적 XP = `50 × (N − 1) × N`.

| 레벨 | 도달 누적 XP | 외형 해금 |
| --- | ---: | --- |
| 1 | 0 | 기본 캐릭터 |
| 5 | 1,000 | 아우라 |
| 10 | 4,500 | 망토 |
| 15 | 10,500 | 특별 이펙트 |

최고 레벨은 15다. 15레벨 도달 이후 XP 총합 저장을 계속할지 상한을 둘지는 팀과 합의한다. 이미 DB에 저장된 레벨이 XP 환산 레벨보다 높아도 기존 레벨을 자동 하향하지 않는다.

## 서버 연동 계약 (미구현·조율 필요)

- Unity 클라이언트가 `reward_xp`, 승패, 사용자 ID를 임의로 보내는 것만으로 DB XP를 올리지 않는다.
- 퀴즈/탈출의 **신뢰할 수 있는 결과 발생 지점**이 종료 이벤트마다 UUID 등 고유 이벤트 ID를 생성해 검증 및 기록한다.
- 서버는 access token을 Supabase Auth로 검증해 계정 주체를 확정한다.
- `xp_events(user_id,source,source_event_id)`의 중복 제약으로 재전송 시 한 번만 지급한다.
- 서버의 단일 트랜잭션에서 XP/레벨을 갱신하고 신규 레벨·누적 XP·지급 XP·중복 여부를 반환한다.
- `AccountProfile` 응답에 XP 정보를 추가할 때 이전 응답 및 계정 HUD 소비자와 호환성·배포 순서를 검토한다.
- 서버 승인 전까지 현재의 409/55000 지급 차단은 유지한다.

## 외형 보상 연동 (미구현·조율 필요)

서버에서 조회한 로그인 계정의 레벨로 아우라(5), 망토(10), 특별 이펙트(15) 사용 가능 여부를 계산한다. 로그인/로그아웃/계정 전환 때 아바타 상태를 다시 설정해야 한다. Sunbox 무료 에셋 설치 유무를 모두 지원하고, Sunbox 원본 또는 파생 파일을 라이선스 확인 없이 공개 저장소에 커밋하지 않는다.

## 검증 예정

1. 경계값: Lv.1 0 XP, Lv.5 1000 XP, Lv.10 4500 XP, Lv.15 10500 XP.
2. Lv.15 초과 방지, 0/16 레벨 및 음수 XP 입력 거절, 기존 저장 레벨 비하향.
3. QuizWin/Loss/Draw와 EscapeSuccess/Failure 각각 XP 값 검사.
4. 신뢰된 서버 결과만 보상; 같은 이벤트 중복 수신/재접속/타인 계정 요청 차단.
5. Unity 6000.4.5f1 설치·미설치 Sunbox 환경 검증, 기존 커스터마이징/인벤토리 회귀 검사.
6. `python Tools/team/check.py --base origin/main` 및 PR 팀 검사.

**현재 이 문서는 구현 설계 기록이며, CI나 Unity 검증 완료를 의미하지 않는다.**


## 계정 레벨 읽기 브리지 — 구현됨 (아직 Unity 실행 검증 전)

소스: `Assets/TeamProgression/AccountLevelUnlocks.cs` 및 동일 이름 `.meta`.

- 씬·Player 수정 없이 Unity 시작 시 `Campus level unlocks` 오브젝트가 자동 생성된다.
- 기존 `CampusCommons.Account.AccountGate.EntryReady`와 `EntryRevoked` 이벤트를 구독한다. 프로필 조회 갱신 시 `AccountSession.Changed`도 반영한다.
- `AccountGate.Ready`, 인증 상태, 반환된 `user_id`와 현재 세션 ID의 일치, 레벨 1~15를 모두 확인한 뒤 **외형 표시용으로만** 저장한다.
- 로그인 전/로그아웃/재인증 실패/씬 전환에는 계정/레벨 캐시를 비운다. 다른 계정의 외형을 보여주지 않는다.
- 외부 소비자는 `AccountLevelUnlocks.Instance.IsReady`, `CurrentLevel`, `AuraUnlocked`, `CapeUnlocked`, `SpecialEffectUnlocked`, `Changed` 이벤트를 읽는다.
- 서버의 실제 XP 지급/레벨 수정과 무관하다. `AccountProfile` 응답에는 아직 XP가 없고 기존 지급 차단 409/55000은 유지한다.

### Unity 확인 순서 (6000.4.5f1)

1. 수정 파일이 있다면 먼저 보존한다. 터미널에서 `git status`를 보고, 정리된 상태에서 `git fetch origin` → `git switch feat/cc-32-level-progression` → `git pull --ff-only origin feat/cc-32-level-progression`을 실행한다.
2. 프로젝트를 Unity 6000.4.5f1로 열고 `Assets/TeamProgression/`의 `LevelProgressionRules.cs`, `AccountLevelUnlocks.cs`와 각 `.meta`를 확인한다.
3. `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity` 씬을 열고 Console에 컴파일 오류가 없는지 확인한다.
4. Play 후 로그인/닉네임 입력/로비 입장을 완료한다. Console에 `[CampusCommons] Account level loaded: Lv.1 (cosmetic display only).` 등 본인 레벨 로그가 표시되어야 한다. 민감 정보는 로그에 출력하지 않는다.
5. 게임에서 로그아웃한 뒤에는 해금 상태가 초기화되어야 한다. 이후 서로 다른 계정으로 입장하면 해당 계정 레벨을 기준으로 다시 계산한다.
6. 이 단계에서 아우라/망토/이펙트 모델은 아직 자동 표시되지 않는다. 별도 외형 프리팹/오브젝트 연결이 후속 작업이다. 로그인만으로 XP는 지급되지 않는다.

### 후속 외형 컴포넌트 연결 예시

```csharp
var unlocks = CampusCommons.TeamProgression.AccountLevelUnlocks.Instance;
if (unlocks != null && unlocks.IsReady)
{
    bool canUseAura = unlocks.AuraUnlocked;
    bool canUseCape = unlocks.CapeUnlocked;
    bool canUseSpecial = unlocks.SpecialEffectUnlocked;
}
```

실제 표시 오브젝트는 게임 플레이어의 아바타 자식에 연결해야 하며, `Changed` 발생 시 토글하고 로그아웃 시 반드시 숨긴다. 이 상태는 클라이언트 UI 표시 용도이며 XP 지급/서버 권한 근거가 아니다.
