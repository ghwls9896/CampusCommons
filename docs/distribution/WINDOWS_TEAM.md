# Windows에서 Unity로 개발하고 실행하기

## 1. 기존 내 작업을 보존하고 받기

#55가 병합된 뒤 처음 받는 팀원은 Git을 설치하고 PowerShell에서 최신 `main`을 받습니다.

```powershell
git clone https://github.com/ghwls9896/CampusCommons.git
cd CampusCommons
```

기존 작업 브랜치가 있는 팀원은 자신의 변경을 커밋한 뒤 해당 브랜치에서 `git fetch origin`과 `git merge origin/main`으로 반영합니다. 새 작업은 최신 `origin/main`에서 이슈별 브랜치로 시작합니다. [팀 작업 절차](../../CONTRIBUTING.md)

Unity Hub에서 프로젝트를 추가해 **6000.4.5f1**로 열고 `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity`에서 **Play**를 누릅니다. 같은 로비와 퀴즈룸을 테스트할 때는 각자 다른 계정으로 로그인합니다. 계정 연결은 저장소의 공개 `AccountConnection.json`을 사용하고, 2인 퀴즈는 로그인으로 받은 임시 좌석 티켓을 사용합니다.

## 2. 선택 사항 Windows 게임 빌드

Exe가 필요한 경우 Windows Standalone Build Support와 [uv](https://docs.astral.sh/uv/getting-started/installation/)를 설치합니다. 이전 작업 폴더에 `Assets/QuizRoom/Resources/QuizConnection.json`이 있으면 Unity 프로젝트 밖으로 옮깁니다. 빌드 검사는 해당 파일이 Unity Resources에 들어 있으면 중단합니다. Unity Editor를 닫고 저장소 루트에서 아래 명령을 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File Tools/distribution/build-windows.ps1
```

스크립트가 전용 Python 3.12.11, 의존성, QEV 모델을 준비하고 경로 재배치·오프라인 추론 검사 후 Unity 게임을 만듭니다. 빌드 과정에는 인터넷과 여유 디스크 공간이 필요합니다. 플레이어에게 Python/pip 명령을 실행시키지 않습니다.

Unity 설치 경로가 다르면 `-UnityEditor "D:\Unity\6000.4.5f1\Editor\Unity.exe"`를 지정합니다. 실패하면 `.distribution-work/unity-windows-build.log`와 PowerShell 오류를 전달하고, 키/토큰/비밀번호/실제 학습 PDF는 로그에 첨부하지 않습니다.

결과는 `Builds/CampusCommons-windows-x64/` **전체 폴더**입니다. 그 폴더를 ZIP으로 공유합니다. exe만 보내면 데이터와 모델을 찾을 수 없습니다. 배포 ZIP을 받은 일반 플레이어는 압축을 풀고 `CampusCommons.exe`를 실행하면 됩니다. 첫 실행 자동 준비와 카메라 허용 후 이용하며 로그인·퀴즈에는 인터넷이 필요합니다.

## 3. Windows 확인 항목

- Python을 따로 설치하지 않은 환경에서 최초 자동 준비/두 번째 실행 재사용.
- 회원가입·이메일 확인 또는 기존 계정 로그인 → 닉네임 → 로비.
- 다른 계정과 서로 다른 두 좌석 → 각자 PDF 선택과 범위 지정 → 3단계 퀴즈 → 결과/퇴장.
- 좌석 E → 카메라 미리보기만 켜짐 → 시작 → 판별 → 종료·일어나기.
- 기존 캐릭터 조작, 탈출룸 입장·기존 상호작용이 유지되는지.
- 카메라 거절·설치 중 종료 후 재실행 시 복구, 다른 게임 창이 분석 중일 때 복구 차단.
