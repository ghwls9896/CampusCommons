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


## 레벨별 캐릭터 외형 1차 구현 (Unity Play Mode 검증 전)

`Assets/TeamProgression/LevelCosmeticVisuals.cs`는 공유 `Player.cs`를 수정하지 않고, 로그인 완료 시 플레이어의 `Student character` 하위에 표시용 오브젝트를 자동 생성한다. Sunbox 에셋을 직접 참조하지 않아 미설치 환경에서도 컴파일하도록 설계했다. 생성된 재질·망토 메시·파티클은 런타임 오브젝트이며 파일로 저장되지 않는다.

| 레벨 | 초안 표시 |
|---|---|
| Lv.1–4 | 기본 캐릭터, 보상 외형 없음 |
| Lv.5–9 | 발밑 푸른 원형 룬 2개 + 작은 상승 빛 점 |
| Lv.10–14 | Lv.5 아우라 + 어깨에서 내려오는 파란 망토, 금색 테두리 |
| Lv.15 | Lv.10 외형 + 금빛 마법진, 양쪽 날개 윤곽, 금색 빛 점 |

`LevelCosmeticVisuals` 컴포넌트에는 아우라 색상 Blue/Purple/Green, 망토 색상 Blue/Red/White가 있다. 이 색상은 아직 사용자 계정에 저장되지 않는다. Unity 에디터에서는 **로그인 후에만** `Preview In Editor`를 체크하고 `Preview Level` (1~15)을 바꿔 눈으로 확인할 수 있다. `#if UNITY_EDITOR`로 보호된 로컬 미리보기이므로 실제 계정 XP/레벨, 서버 보상과는 무관하며 빌드에는 포함되지 않는다.

### 실제 테스트 순서 (Windows / Unity 6000.4.5f1)

1. GitHub Desktop에서 담당 브랜치 `feat/cc-32-level-progression`을 유지하고 `Fetch origin` → `Pull origin`한다. 로컬 `ProjectSettings/ProjectSettings.asset`의 `CAMPUS_SUNBOX_AVATAR` 미커밋 설정과 `.git/info/exclude`, Sunbox 원본 에셋은 보존하고, 충돌/overwrite 경고 시 진행을 중단한다.
2. Unity 에디터에서 `Assets/TeamProgression/LevelCosmeticVisuals.cs`와 `.meta`를 확인하고 컴파일 오류가 없는지 검사한다.
3. `GrandLibrary_Living` 씬 Play → 실제 계정 로그인 → 닉네임 입력 → 로비 입장. (처음 Lv.1이라면 외형 효과가 없는 것이 정상.)
4. Play 상태 Hierarchy에서 `Campus level cosmetics` GameObject를 찾아 클릭한다. Inspector의 `Level Cosmetic Visuals`에서 `Preview In Editor`를 체크한다.
5. `Preview Level` 5 → 10 → 15 순서로 바꾼다. Game 화면/Hierarchy에서 마법진 → 망토 → 금색 날개·파티클을 확인한다. `Aura Palette`, `Cape Palette`로 각각 세 가지 색상도 볼 수 있다.
6. 체크를 해제하면 실제 서버 계정 레벨에 맞춘 외형으로 돌아간다. 로그아웃하면 효과가 전부 숨겨지는지 확인한다.
7. Play 종료 후에도 계정 DB·실제 레벨·XP는 바뀌지 않는다. 에디터 인스펙터 미리보기 값은 영구 저장하지 않는다.

### 구현 경계/제약

- 이미지로 만든 레벨별 외형은 컨셉 시안이다. 현재 구현은 Unity 기본 메시·LineRenderer(움직이는 빛 점 포함)만 사용한 **1차 3D 프로토타입**이며 망토 물리·고급 날개 메시·발광 포스트프로세싱은 아직 없다.
- 망토 높이/길이는 모델마다 다를 수 있으며 착석/성별/헤어 프리셋과의 겹침은 Unity에서 확인 후 수정한다.
- 모델은 1인칭일 때 기존 Player 설정에 따라 숨겨지므로 외형 효과도 함께 숨겨진다.
- 이 스크립트와 원본 레벨 규칙은 캐릭터를 꾸미는 **표시 기능**이다. 보상 지급 API는 계속 비활성화 상태이다.
- 실제 Lv.5/10/15 자동 해금 동작, Sunbox 설치/미설치, 씬 이동, 로그아웃/다른 계정 로그인, 성능은 사용자 Unity 테스트가 남아 있다.
- 새 코드는 `Assets/TeamProgression/`만 수정한다. Sunbox 원본/복제 에셋이나 프로젝트 공통 설정은 공개 저장소에 추가하지 않는다.

### Unity Safe Mode에서 보고된 컴파일 오류 조치 (2026-11-17)

- 사용자 Unity 6000.4.5f1에서 `LevelCosmeticVisuals.cs`의 `ParticleSystem` 참조에 대한 CS1069 오류가 확인되었다.
- 공유 `Packages/manifest.json`에는 Particle System 모듈이 없으므로, 팀 공통 패키지를 변경하지 않고 외형 프로토타입의 빛 입자를 `LineRenderer` 기반 이동 광점으로 바꿨다. 이 수정은 작업 브랜치 커밋 `84153da`에만 있다.
- 사용자 PC는 Unity Safe Mode 상태이므로 재차 `git pull`하고 컴파일/플레이 검증하기 전까지 완료로 간주하지 않는다.
- 동시에 보고된 `CampusCommons.LibraryCharacter` 네임스페이스 누락 오류는 외형 파일과 무관하다. `Assets/LibraryCharacter/Scripts/Monster.cs` 등 파일은 Windows 파일시스템에 존재하지만 Unity Project 창에 폴더가 누락되는 현상이다. 원인을 확인하기 전에는 파일 또는 `.meta`를 이동/삭제하지 않는다.
