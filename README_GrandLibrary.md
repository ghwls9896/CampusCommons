# Grand Library — 포탈형 학생 교류 공간

## Unity에서 열기

1. Unity Hub에서 프로젝트 폴더 `CampusCommons`를 엽니다. 사용한 Unity 버전은 **6000.4.5f1**입니다.
2. Project 창에서 **Assets → GrandLibrary → Scenes → GrandLibrary** 씬을 더블클릭합니다.
3. 상단 **▶ Play**를 누르고 Game 창을 클릭합니다.

기존 `Assets/Campus/Scenes/CampusCommons.unity`는 초기 버전으로 남겨 두었습니다. 새 공간을 보려면 `GrandLibrary.unity`를 여세요.

## 공간과 포탈

- **라운지:** 3개 층의 원형 목재 서가와 곡선 계단, 공부용 테이블 두 곳과 가까이 배치한 의자, 소파와 조명. 포탈은 라운지 오른쪽에 모아 두었습니다.
- **MAFIA:** 실제 가구 모델로 만든 10개 좌석과 원형 테이블이 있는 마피아방.
- **QUIZ 1 VS 1:** 실제 책상과 의자 두 세트, 정면 화면이 있는 퀴즈방.
- **ESCAPE ROOM:** 사용자가 제공한 `Downloads/baldi/source/School.fbx`와 텍스처로 만든 학교 공간. 원본의 교실과 복도를 포함합니다.

라운지 오른쪽에서 원하는 타원형 포탈 안으로 **걸어 들어가면** 해당 방으로 이동합니다. `ESCAPE ROOM` 포탈은 제공된 학교 모델 안의 교실로 연결됩니다. 각 방과 학교 안의 `LOUNGE` 포탈을 통과하면 라운지로 돌아옵니다.

## 조작

| 입력 | 동작 |
|---|---|
| WASD / 방향키 | 이동 |
| 왼쪽 Shift | 달리기 |
| 마우스 오른쪽 버튼을 누른 채 이동 | 카메라 회전 |
| E | 가까운 좌석에 앉기 / 일어나기 |
| R | 손 흔들기 |

## 편집할 파일

- `Assets/GrandLibrary/Models/GrandLibrary.fbx`: 라운지와 마피아·퀴즈방의 3D 메시. Blender에서 제작하여 Unity로 가져온 모델입니다.
- `Assets/GrandLibrary/UserSchool/Models/BaldiSchool.fbx`: 제공된 학교 메시를 재질별로 묶어 가져온 모델. 텍스처는 `UserSchool/Textures`에 있습니다.
- `Assets/GrandLibrary/Realism`: 실제 소파·의자·테이블·책장·조명 FBX와 텍스처. 재질별 출처가 `sources.json`에 있습니다.
- `Design/GrandLibrary/GrandLibrary.blend`: 라운지와 게임방의 건축 메시를 편집할 수 있는 Blender 원본. 외부 가구와 제공된 학교는 별도 FBX로 Unity 씬에 배치되어 있습니다.
- `Assets/GrandLibrary/Scenes/GrandLibrary.unity`: 모델, 조명, 좌석과 포탈 충돌 영역이 배치된 씬.
- `Assets/GrandLibrary/Scripts/LibraryPortal.cs`: 포탈 통과 시 목적지 이동.
- `Assets/GrandLibrary/Scripts/LibraryPlayer.cs`: 로컬 캐릭터와 카메라 조작.
- `Tools/build_grand_library.py`: Blender 4.4에서 메시와 텍스처를 재생성하는 스크립트. 실행 시 FBX와 Blender 원본을 다시 만듭니다.
- `Design/GrandLibrary/unity-*.png`: Unity에서 렌더한 각 공간의 미리보기.

각 방의 FBX 메시는 같은 재질과 충돌 속성을 기준으로 묶여 있습니다. 상세한 메시 편집은 Blender 원본에서 할 수 있고, 포탈 목적지와 좌석·조명은 Unity Hierarchy에서 직접 수정할 수 있습니다. 생성 메뉴 `Campus → Create grand library scene`은 새 씬의 배치를 재생성하므로 직접 수정한 씬은 다른 이름으로 저장하세요.

## 현재 구현 범위

현재는 1명이 둘러볼 수 있는 Unity 로컬 공간입니다. 캐릭터 이동, 의자 상호작용, 포탈 왕복이 구현되어 있습니다. 제공된 `baldi` 폴더에는 학교 FBX와 텍스처가 들어 있어 이를 연결했습니다. 실행 가능한 게임 코드나 AI는 포함되어 있지 않았으므로 추격과 탈출 게임 규칙은 아직 없습니다. 원본의 고정된 닫힌 문 표면은 학교를 걸어서 둘러볼 수 있도록 제거했습니다. 실제 온라인 1대1 접속·문제 출제·채점과 마피아 역할·투표도 아직 구현하지 않았습니다.

소파, 의자, 테이블, 책장, 조명과 목재 질감에는 [Poly Haven](https://polyhaven.com/)의 [CC0 에셋](https://polyhaven.com/license)을 사용했습니다. 학교 모델과 텍스처는 사용자가 제공한 별도 자료입니다.

`Design/GrandLibrary/unity-validation.txt`에는 자동 Play Mode 검사 결과가 저장됩니다.
