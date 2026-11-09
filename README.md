# Campus Commons — 학생 교류 공간

## 팀 협업부터 시작하기

모든 팀원과 AI 에이전트는 [공통 지침](AGENTS.md), [개발 절차](CONTRIBUTING.md), [담당표](docs/team/OWNERSHIP.md)를 먼저 읽습니다. 기존 작업을 새 규칙으로 옮기는 방법과 복사할 에이전트 시작 프롬프트는 [팀 시작 안내](docs/team/START_HERE.md)에 있습니다. **main 직접 수정 대신 작업 Issue → 독립 브랜치/폴더 → PR → 검토 후 통합**으로 진행합니다.

## 팀원 시작하기

1. 저장소를 클론한 뒤 Unity Hub에서 이 폴더를 프로젝트로 추가합니다.
2. **Unity 6000.4.5f1**로 열고 `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity` 씬을 엽니다.
3. Play를 눌러 실행합니다. 첫 실행에는 Unity가 에셋을 가져오는 시간이 걸릴 수 있습니다.

기본 조작은 WASD 이동, Shift 달리기, V 시점 전환, E 상호작용입니다. 1인칭에서는 마우스를 움직여 시점을 조절하고, 3인칭에서는 마우스 오른쪽 버튼을 누른 채 시점을 조절합니다. 탈출방에서는 클릭 또는 E로 상호작용합니다.

`Library`, `Temp`, 로컬 씬 백업 등은 업로드하지 않으며 Unity가 필요한 파일을 다시 생성합니다. 아래 설명은 이전 버전의 기록입니다.

> **새 버전:** 사진을 참고해 만든 원형 서재형 라운지와 세 포탈은 `Assets/GrandLibrary/Scenes/GrandLibrary.unity`에 있습니다. 실행 방법과 구성은 [그랜드 라이브러리 안내](README_GrandLibrary.md)를 먼저 확인하세요. 아래 내용은 보관된 초기 씬 `CampusCommons.unity`에 대한 설명입니다.

넓은 라운지, 퀴즈방, 마피아방이 열린 출입구로 연결되는 Unity용 3D 기본 공간입니다. 모든 가구와 캐릭터는 이 프로젝트에서 만든 기본 도형으로 구성되어 별도 유료 에셋이 필요 없습니다.

## 시작하기

1. 설치된 **Unity Editor 6000.4.5f1**로 엽니다. 제작 중 실제 설치 경로를 확인하고 해당 버전에서 씬 생성·컴파일·Play Mode 검사를 마쳤습니다.
2. Hub의 **Projects → Add → Add project from disk**에서 이 `CampusCommons` 폴더를 선택합니다. `Assets` 폴더가 아닌 상위 프로젝트 폴더를 선택하세요.
3. `Assets/Campus/Scenes/CampusCommons.unity`를 더블클릭합니다. 편집 가능한 씬과 `CampusEnvironment.prefab`이 이미 포함되어 있습니다.
4. 원래 배치를 다시 생성하려면 상단 **Campus → Create editable campus scene**을 실행합니다.
5. **Play**를 누르고 Game 창을 클릭합니다.

씬 생성 후 Hierarchy의 **Campus Commons → Lounge / Quiz / Mafia** 아래에서 가구를 직접 옮기고 수정할 수 있습니다. 생성 메뉴를 다시 실행하면 원래 배치로 덮어쓰므로, 수정본은 다른 씬 이름으로 저장하세요. 플레이 중의 변경은 저장되지 않습니다.

## 구성

| 공간 | 크기 | 내용 |
|---|---|---|
| 라운지 | 36 × 28m | 소파 대화 공간 3곳, 커피 테이블, 스터디 테이블, 책장, 안내 데스크, 화분 |
| 퀴즈방 | 16 × 16m | 12인 좌석·책상, 중앙 통로, 무대, 문제 표시용 화면 |
| 마피아방 | 16 × 16m | 10인 원형 좌석, 토론 테이블, 진행 표시용 화면 |

건물 높이는 3.4m이고 모든 바닥은 연결됩니다. 출입구 폭은 3m입니다. 초기 편집과 3인칭 카메라를 위해 천장은 열어 두었습니다. 길이 단위는 Unity 1 unit = 1m입니다.

## 조작

| 입력 | 동작 |
|---|---|
| WASD / 방향키 | 이동 |
| 왼쪽 Shift | 달리기 |
| 마우스 오른쪽 버튼 누른 채 이동 | 카메라 회전 |
| E | 가까운 좌석에 앉기 / 원래 위치로 일어나기 |
| R | 손 흔들기 |
| M | 공간 선택 지도 열기 / 닫기 |
| Esc | 지도 닫기 |

지도 버튼은 확인용 순간 이동입니다. 실제 출입구를 걸어서 통과해도 됩니다.

## 파일

- `Assets/Campus/Resources/campus.json`: Unity와 Blender가 공유하는 배치·재질·좌석 데이터
- `Assets/Campus/Scripts`: 기본 캐릭터, 이동, 카메라, 로컬 좌석 상호작용
- `Assets/Campus/Editor`: 편집 가능한 씬·환경 프리팹 생성기
- `Design/CampusCommons.blend`: Blender 4.4로 생성한 공간 원본. Unity 자동 변환을 피하기 위해 Assets 밖에 보관합니다.
- `Design/Campus-overview.png`, `Design/Lounge-detail.png`: Blender에서 렌더한 공간 미리보기. Unity 실행 화면은 아닙니다.
- `Design/Unity-overview.png`: 실제 Unity 재질과 조명으로 렌더한 공간 미리보기
- `Tools/build_world.py`: Blender에서 원본과 JSON, 미리보기를 재생성하는 도구
- `Tools/validate_world.py`: 배치·출입구·좌석 접근성 검사

씬은 Unity 내장 렌더 파이프라인의 Standard 재질을 사용합니다. 이 프로젝트를 그대로 열면 됩니다. 다른 URP 프로젝트에 복사할 경우 해당 파이프라인용 재질 변환이 필요합니다. 입력은 기존 Input Manager를 사용하므로 다른 프로젝트에 가져올 때 Active Input Handling을 Input Manager 또는 Both로 설정하세요.

## 현재 구현 범위와 다음 단계

현재는 **1인 로컬 공간 프로토타입**입니다. 기본 학생 아바타 1개와 이동·걷기 모션·손 흔들기·앉기·공간 이동을 구현했습니다. 화면의 참가자 수나 온라인 사용자를 모사하지 않습니다.

여러 학생의 실제 동시 접속, 음성·텍스트 채팅, 인증, 방 생성·참가, 서버의 좌석 점유 관리, 퀴즈 출제·채점, 마피아 역할 배정·투표·낮밤 진행은 아직 구현하지 않았습니다. 이 공간의 좌석 Transform과 방별 그룹을 해당 기능의 배치 지점으로 사용할 수 있습니다. 좌석 점유 필드는 로컬 시연용이므로 네트워크 도입 시 서버 권한으로 교체해야 합니다.

## 검증 환경

Unity Editor `6000.4.5f1`에서 스크립트 컴파일과 씬·프리팹 생성을 완료했습니다. Play Mode에서 캐릭터 생성, 바닥 접촉과 이동, 43개 좌석 생성, 좌석 점유·앉기·일어나기, 양쪽 출입구 통과를 검사했습니다. 0.25m 간격의 배치 검사에서도 모든 좌석에 상호작용 거리 내로 접근할 수 있었습니다. `Design/unity-validation.txt`와 `Design/validation.json`에 결과가 있습니다.

자동 검사 중 Unity Editor의 검색 인덱스 시작 코드(`UnityEditor.Search.SearchDatabase`)에서 별도 예외가 발생했습니다. 이 batch mode 편집기 내부 오류는 공간의 Play Mode 검사와 구분해 결과 파일에 기록했습니다. 수동 키보드·마우스 조작, 실행 파일 빌드, 실제 여러 명의 접속은 검사하지 않았습니다.

Blender 재생성 명령(PowerShell):

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' --background --python .\Tools\build_world.py
```
