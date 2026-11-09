# 담당과 수정 경계

2026-11-09 팀장이 알려준 담당 기준입니다. 실제 작업 진행/점유 상태는 [열린 Issues](https://github.com/ghwls9896/CampusCommons/issues)와 [Draft 포함 PR](https://github.com/ghwls9896/CampusCommons/pulls)에서 확인합니다.

| 담당 | 기능 | 현재 관련 경로 / 새 기능 기본 위치 |
|---|---|---|
| `Yeni-sjs` | 교수를 찾아라룸(마피아룸) | 새 구현 `Assets/MafiaRoom/` (예정 경로, 현재 구현 완료 의미 아님) |
| `HIGHCHOI` | 퀴즈룸 | `Assets/QuizRoom/`, 퀴즈 백엔드 `backend/quiz.py`, 관련 테스트 |
| `HIGHCHOI` | 웹캠, 회원가입/로그인, 개인 DB | 새 구현 `Assets/Webcam/`, `Assets/Account/`, `backend/auth/`, `backend/profiles/` (예정 경로) |
| `ghwls9896` | 전체 공간, 탈출룸 | `Assets/StudyImmersive/`, `Assets/GrandLibrary/` 공간 에셋·씬, `Assets/LibraryCharacter/`, 기존 `Assets/Campus/` |
| `GongBeak` | 캐릭터 | 현재 `Assets/GrandLibrary/Scripts/Player.cs`의 아바타·이동·카메라, 새 분리 모듈 `Assets/Characters/` (예정 경로) |

`Assets/LibraryCharacter/`는 이름과 달리 현재 학교/탈출룸 관련 코드가 많으므로 이름만 보고 캐릭터 담당이 전부 수정하지 않습니다. `Player.cs`에는 캐릭터와 방 입장/퇴장/UI가 섞여 있어 단독 소유 파일이 아닙니다. 폴더 분리는 작업별로 점진적으로 수행하고 이번 협업 설정에서 게임 코드를 재구성하지 않습니다.

공간/씬 통합 담당은 저장소 소유자 `ghwls9896`입니다. `CODEOWNERS`는 각 기능 담당 + 통합 검토자를 지정하며, 공간·공통 설정에는 `HIGHCHOI`를 추가 검토자로 둡니다. 이는 리뷰 경로이며 다른 사람 기능의 구현 담당을 바꾸지 않습니다. 담당표 변경도 PR로 합니다.

## 공유 파일 조율

다음은 수정 전에 기존 Issue/PR 확인과 영향받는 담당자의 조율이 필요한 파일입니다.

- 모든 `.unity`, `.prefab`: 특히 `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity`.
- `Assets/GrandLibrary/Scripts/{Player,GameStart,Portal,LobbyDoor}.cs`.
- `ProjectSettings/`, `Packages/`, `backend/app.py`, `render.yaml`.
- 공통 팀 규칙 `AGENTS.md`, `.github/`, `Tools/team/`.

1. Issue에 **파일 경로, 변경 의도, 작업자, 예상 인계 시점**을 적고 열린 PR의 변경 파일/작업 JSON을 확인합니다.
2. 같은 씬/프리팹을 수정 중인 작업이 있으면 통합 담당자가 순서를 정합니다. 먼저 작업한 PR을 반영한 뒤 다음 작업을 시작합니다. 다른 영역 구현은 계속할 수 있습니다.
3. 공통 스크립트도 동시 수정 지점과 API 계약을 확인합니다. 조율 내용과 상대방의 확인을 Issue에 남깁니다. 확인이 없는데 공유 파일을 독점했다고 선언하지 않습니다.
4. 해당 task JSON의 `shared_files`에 실제 수정할 공유 파일을 **정확한 경로**로 열거합니다. CI는 누락만 검사하며 이 기록을 승인/분산 락으로 취급하지 않습니다.
5. 인수인계 또는 통합 시 Issue에 공유 파일 작업 종료/다음 담당을 기록합니다. 취소된 작업도 해제 상태를 남깁니다.

단순 선언만으로 동시성은 막히지 않습니다. 최종 방어는 서로 다른 working directory, 최신 원격 확인, 공통 파일 작업 순서, 사람 리뷰 및 main 보호입니다.

## 팀 간 인터페이스

공용 Player/룸 진입 지점이나 서버 route/JSON을 바꾸는 작업은 Issue에 `호출자 → 인터페이스 → 구현자`, 필드/메서드 이름, 오류 형태, 호환 여부, 먼저 합칠 PR을 적습니다. 예: 퀴즈의 `POST /v1/quizzes`와 클라이언트 JSON 필드는 `HIGHCHOI` 작업에서 함께 유지하고, 공통 Player 접점은 `GongBeak`/`ghwls9896`와 조율합니다.

기능별 테스트용 씬/프리팹을 자기 폴더에서 준비하고 main 씬 연결은 작은 통합 변경으로 분리하는 것을 기본으로 합니다. 로그인/개인 DB는 서비스 자격증명을 서버에 두며 실제 사용자 데이터/비밀번호/카메라 녹화물을 저장소나 PR에 올리지 않습니다.
