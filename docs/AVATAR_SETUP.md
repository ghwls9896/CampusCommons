# CampusCommons — 3D 아바타 설치 및 사용 가이드

## 1. 작업 개요

기존 CampusCommons 도서관의 기본 도형 캐릭터를 Unity Asset Store의 **Stylized Customizable Avatars FREE** 기반 캐릭터로 교체하고 커스터마이징 기능을 개발했습니다.

- 담당자: GongBeak
- Issue: #19
- Draft PR: #20
- 작업 브랜치: `feat/cc-19-avatar-customization`
- 개발 Unity 버전: `6000.4.5f1`

### 구현 기능

- 남성·여성 캐릭터 선택
- 헤어스타일 및 머리색 변경
- 피부색 변경
- 상의·하의·신발·모자·안경 변경
- 걷기·달리기 애니메이션
- R키 손 흔들기
- E키 착석 자세 및 위치 보정
- C키 커스터마이징 메뉴
- 실시간 캐릭터 미리보기 기능 코드

## 2. 사용 에셋 및 필수 환경

**Stylized Customizable Avatars FREE**

https://assetstore.unity.com/packages/3d/characters/humanoids/stylized-customizable-avatars-free-230391

Unity에서 다음 항목을 준비해야 합니다.

1. `Window → Package Manager → My Assets`에서 해당 에셋을 Import합니다.
2. Unity UI(uGUI) 패키지 `com.unity.ugui`가 설치되어 있는지 확인합니다.
3. `Window → Package Manager → Built-in → Animation` 모듈이 활성화되어 있는지 확인합니다.

**주의:** 현재 캐릭터 스크립트들은 `Sunbox.Avatars` 네임스페이스를 사용합니다. 해당 에셋을 설치하지 않으면 Unity에서 컴파일 오류가 발생할 수 있습니다.

## 3. 파일 구조

팀에서 직접 개발한 스크립트는 다음 위치에 있습니다.

```text
Assets/
└── TeamAvatar/
    ├── LibraryAvatarMotion.cs
    ├── LibraryAvatarCustomizerUI.cs
    ├── LibraryAvatarSitPose.cs
    └── LibraryAvatarPreview.cs
```

각 파일의 `.meta` 파일도 함께 유지해야 합니다.

별도로 필요한 아바타 프리팹과 Animator Controller의 경로는 다음과 같습니다.

```text
Assets/
└── Resources/
    └── TeamAvatar/
        ├── LibraryAvatar.prefab
        └── LibraryMovement.controller
```

이 두 파일은 Sunbox 에셋에서 파생된 파일이므로 라이선스 확인 없이 공개 저장소에 재배포하지 않습니다.

## 4. LibraryAvatar 프리팹 준비

1. Project 창에서 `t:Prefab Avatar`로 검색합니다.
2. Sunbox 에셋의 Avatar 프리팹을 찾습니다.
3. `AvatarCustomization` 컴포넌트가 연결되어 있는지 확인합니다.
4. 프리팹을 복제하고 이름을 `LibraryAvatar`로 변경합니다.
5. `Assets/Resources/TeamAvatar/` 폴더에 복제본을 넣습니다.

런타임에서는 다음 경로로 프리팹을 불러옵니다.

`Resources.Load<GameObject>("TeamAvatar/LibraryAvatar")`

프리팹을 준비한 후 아바타에 남성·여성 모델과 의상 참조가 정상적으로 연결되어 있는지 확인해야 합니다.

## 5. LibraryMovement 컨트롤러 준비

1. `Assets/SunboxGames/Avatars/Animations` 폴더를 엽니다.
2. `AvatarAnimator_Locomotion.controller`를 찾습니다.
3. `Ctrl + D`로 복제합니다.
4. 이름을 `LibraryMovement`로 변경합니다.
5. `Assets/Resources/TeamAvatar/` 폴더에 넣습니다.

사용하는 Animator 파라미터:

| 파라미터 | 기능 |
|---|---|
| MoveX | 좌우 이동 |
| MoveY | 앞뒤 이동 |
| Wave | 손 흔들기 |
| IsSitting | 착석 상태 |
| Blend | 기존 블렌드 트리 구성 요소 |

현재 달리기는 별도 Run 애니메이션 대신 이동 애니메이션 재생 속도 증가 방식으로 구현했습니다.

## 6. Unity Input Manager 설정

`Edit → Project Settings → Input Manager → Axes`

다음 입력 이름이 존재하는지 확인합니다.

| Name | 키 |
|---|---|
| Submit | return |
| Cancel | escape |
| Horizontal | a/d, 좌우 방향키 |
| Vertical | w/s, 상하 방향키 |
| Mouse X | 기존 설정 유지 |
| Mouse Y | 기존 설정 유지 |

필요한 입력만 추가하고 기존 팀 프로젝트의 입력 설정은 임의로 초기화하지 않습니다.

## 7. 남성 캐릭터 오류 해결

Sunbox 에셋 테스트 중 남성 캐릭터를 변경하면 다음 오류가 발생했습니다.

`NullReferenceException — AvatarCustomization.UpdateHair_Internal()`

수정 대상:

`Assets/SunboxGames/Avatars/Scripts/AvatarCustomization.cs`

기존 588행 부근의 모자 검색 코드를 다음과 같이 수정했습니다.

```csharp
UClothingItem hatClothingInstance = CurrentBase
    .GetComponentsInChildren<UClothingItem>()
    .FirstOrDefault(item =>
        item != null &&
        item.ClothingItem != null &&
        item.ClothingItem.SlotType == SlotType.Hat);
```

의상 정보가 비어 있는 항목에 접근하는 것을 방지하는 수정입니다.

이 수정은 공개 GitHub에 Sunbox 원본 파일을 업로드하는 대신, 에셋 설치 후 필요한 경우 직접 적용하도록 안내합니다.

## 8. 착석 위치 보정

기존 `Player.cs`의 착석 기능은 유지하고, 새 아바타의 관절을 조절해 앉는 자세를 구현했습니다.

수정 파일: `LibraryAvatarSitPose.cs`

최종 착석 보정값:

```csharp
[Header("의자 위치 보정")]
public Vector3 sitOffset =
    new Vector3(0f, -0.24f, -0.05f);
```

이 값은 테스트한 도서관 의자에 맞춘 값입니다. 다른 의자 모델에서는 추가 보정이 필요할 수 있습니다.

## 9. Player.cs 연동

현재 아바타 관련 스크립트들은 기존 CampusCommons 플레이어와 연동하도록 개발되었습니다.

Player.cs에서 사용하는 주요 연결 기능:

- `LibraryAvatarMotion` — 걷기, 달리기, 손 흔들기
- `LibraryAvatarCustomizerUI` — 커스터마이징 메뉴
- `LibraryAvatarSitPose` — 앉기 자세 및 위치
- `LibraryAvatarPreview` — 실시간 캐릭터 미리보기

`Player.cs`는 팀 공통 파일이므로 다른 작업자의 변경 사항을 덮어쓰지 않고, 담당자와 조율한 후 최신 버전에 연결 코드를 병합해야 합니다.

**현재 Draft PR 단계에서는 Player.cs 통합이 완료되었다고 간주하지 않습니다.**

## 10. 게임 조작 방법

| 키 | 기능 |
|---|---|
| WASD | 이동 |
| Shift + WASD | 빠른 이동 |
| E | 앉기 / 일어나기 |
| R | 손 흔들기 |
| C | 커스터마이징 열기·닫기 |
| V | 1인칭 / 3인칭 전환 |
| ESC | 커스터마이징 메뉴 닫기 |

커스터마이징 메뉴에서는 성별, 헤어, 머리색, 피부색, 의상 변경을 지원합니다.

현재 외형 변경값의 영구 저장과 멀티플레이 동기화는 구현 범위에 포함되지 않았습니다.

## 11. 테스트 현황

**기존 개발 프로젝트에서 확인된 기능**

- Sunbox 남녀 캐릭터 정상 표시
- C키 커스터마이징
- 헤어·의상 변경
- 걷기 및 손 흔들기
- E키 착석과 위치 보정
- 기존 플레이어 이동 기능

**추가 검증이 필요한 항목**

- 실시간 3D 미리보기 최종 동작 확인
- 최신 팀 브랜치와 Player.cs 병합 후 실행
- 새로운 팀원 PC에서 Sunbox 설치 및 실행
- 여러 의자 모델에서의 착석 위치 검증
- 기존 포털·퀴즈·마피아 기능과 충돌 여부

## 12. 팀 협업 및 라이선스 주의사항

- 본 작업은 Issue #19 및 Draft PR #20에서 관리합니다.
- `Player.cs`는 공유 파일이므로 담당자 협의 후 병합합니다.
- Unity `.meta` 파일은 삭제하거나 GUID를 임의 변경하지 않습니다.
- Sunbox 외부 에셋은 각 사용자가 라이선스 조건에 따라 설치해야 합니다.
- 검증이 완료되면 PR에 테스트 결과를 기록하고 Ready for review로 변경합니다.
- 담당자 승인 없이 main 브랜치에 직접 병합하지 않습니다.
