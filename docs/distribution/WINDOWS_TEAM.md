# Windows 팀원: 통합본 검토와 게임 만들기

현재 제출 브랜치는 `codex/local-integration`, 기준 main은 `0a6c3f7`입니다. 로그인·퀴즈룸·웹캠과 이미 병합된 캐릭터/탈출룸 코드를 포함합니다. Windows x64용 자동 빌드 경로는 구현돼 있지만 실제 Windows 실행은 검증 전입니다.

## 1. 기존 내 작업을 보존하고 받기

검토 중에는 기존 개발 폴더를 덮어쓰지 말고 **새 폴더에** 받는 방법을 권장합니다. Git 설치 후 PowerShell에서:

```powershell
git clone --branch codex/local-integration https://github.com/ghwls9896/CampusCommons.git CampusCommons-integration-review
cd CampusCommons-integration-review
```

이 PR이 main에 병합된 뒤 새로 받는 사람은 `--branch codex/local-integration`을 생략합니다. 기존 작업 브랜치가 있는 팀원은 자신의 변경을 커밋한 뒤 `git fetch origin`과 `git merge origin/main`으로 반영합니다. `reset --hard`, 강제 덮어쓰기나 기존 폴더 삭제는 필요 없습니다.

## 2. 빌드 담당자 1명이 준비할 것

- Unity Hub의 **6000.4.5f1** 및 Windows Standalone Build Support. 프로젝트 버전을 임의로 업그레이드하지 않습니다.
- Git과 [uv 공식 설치](https://docs.astral.sh/uv/getting-started/installation/). uv는 공식 안내대로 `winget install --id=astral-sh.uv -e`로 설치할 수 있습니다. 설치 후 새 PowerShell 창을 엽니다.
- HIGHCHOI에게 기존 `QuizConnection.json`을 비공개로 받아 `Assets/QuizRoom/Resources/QuizConnection.json`에 둡니다. Git에는 예제만 있고 실제 데모 토큰은 제외돼 있습니다. 이 파일은 빌드 시 자동 포함되므로 **완성 게임을 받는 사람마다 전달할 필요는 없습니다**.
- 로그인용 공개 `AccountConnection.json`은 저장소에 포함되어 있습니다. 새 Supabase 프로젝트나 DB를 만들지 말고 기존 연결을 사용합니다. DB/서버 비밀 키는 게임에 넣지 않습니다.
- 팀원 캐릭터와 똑같은 외형까지 확인하려면 GongBeak의 `LibraryAvatar.prefab`, `LibraryMovement.controller`, 각각의 `.meta`와 필요한 에셋 설정이 별도로 필요합니다. 원본 Sunbox 패키지만 설치하면 동일한 프리팹 연결이 자동 생성되는 것은 아닙니다. 현재 통합본의 기본 캐릭터 표시를 코드 삭제로 오해하지 마세요.

## 3. Windows 게임 빌드

이 프로젝트의 Unity Editor를 저장하고 닫은 상태에서, 저장소 루트에서:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/distribution/build-windows.ps1
```

스크립트가 전용 Python 3.12.11, 의존성, QEV 모델을 준비하고 경로 재배치·오프라인 추론 검사 후 Unity 게임을 만듭니다. 빌드 과정에는 인터넷과 여유 디스크 공간이 필요합니다. 플레이어에게 Python/pip 명령을 실행시키지 않습니다.

Unity 설치 경로가 다르면 `-UnityEditor "D:\Unity\6000.4.5f1\Editor\Unity.exe"`를 지정합니다. 실패하면 `.distribution-work/unity-windows-build.log`와 PowerShell 오류를 전달하고, 키/토큰/비밀번호/실제 학습 PDF는 로그에 첨부하지 않습니다.

결과는 `Builds/CampusCommons-windows-x64/` **전체 폴더**입니다. 그 폴더를 ZIP으로 공유합니다. exe만 보내면 데이터와 모델을 찾을 수 없습니다. 배포 ZIP을 받은 일반 플레이어는 압축을 풀고 `CampusCommons.exe`를 실행하면 됩니다. 첫 실행 자동 준비와 카메라 허용 후 이용하며 로그인·퀴즈에는 인터넷이 필요합니다.

## 4. Windows 확인 항목

- Python을 따로 설치하지 않은 환경에서 최초 자동 준비/두 번째 실행 재사용.
- 회원가입·이메일 확인 또는 기존 계정 로그인 → 닉네임 → 로비.
- PDF 선택 → 범위 → 3단계 퀴즈 → 결과/퇴장.
- 좌석 E → 카메라 미리보기만 켜짐 → 시작 → 판별 → 종료·일어나기.
- 기존 캐릭터 조작, 탈출룸 입장·기존 상호작용이 유지되는지.
- 카메라 거절·설치 중 종료 후 재실행 시 복구, 다른 게임 창이 분석 중일 때 복구 차단.

## 승인과 기존 PR

이 통합 PR 작성자는 HIGHCHOI이므로 최종 리뷰어는 **ghwls9896 한 명**입니다. 코드/공유 Player·GameStart 연결과 검사 결과를 확인하고 Approve한 뒤, 필수 CI가 통과하고 충돌이 없는 상태에서 ghwls9896이 Squash merge합니다. Approve만으로 자동 병합되는 것은 아닙니다.

이 통합 PR은 기존 퀴즈 #4, 웹캠 #9, 계정 #10의 변경을 포함합니다. 이 PR을 통합 경로로 선택했다면 세 PR을 다시 별도로 병합하지 말고, 통합 PR이 실제 main에 들어간 뒤 대체된 PR로 정리합니다. 현재 열린 인벤토리 #22는 별도 작업입니다. 최신 #22 커밋 `50c3a5f`와 Git 병합 시뮬레이션은 충돌 없이 통과했지만, 로그인 전/퀴즈 입력 중 I·1~5 입력 및 UI 표시 연동은 추가 검증 대상입니다. Git 충돌 없음이 모든 기능 조합 검증을 뜻하지는 않습니다.
