> 현재 실행본과 검증 범위는 [STATUS.md](STATUS.md)를 먼저 확인하세요. 아래는 재현 가능한 빌드 절차입니다.

# 데스크톱 게임 배포

플레이어는 운영체제에 맞는 **완성 게임 배포본**을 받아 실행합니다. GitHub의 Source code ZIP은 개발 소스이며 완성 게임이 아닙니다.

- macOS: **15.0 이상, Apple Silicon(ARM64)**, Windows: Intel/AMD 64비트(x64).
- 첫 실행에 게임에 동봉된 Python 3.12.11·QEV 모델·라이브러리를 사용자 전용 폴더에 자동 준비합니다. 시스템 Python, PATH, 관리자 권한, pip 명령은 필요 없습니다. Mac 패키지는 압축 약 1.6 GB, 준비 후 약 2.4 GB이며 게임 파일과 별도로 저장 공간이 필요합니다.
- 준비 진행률과 실패 시 재시도를 표시합니다. 준비 중에도 로그인·퀴즈 화면을 쓸 수 있습니다. 다음 실행은 준비된 환경을 재사용합니다.
- 좌석에 앉으면 카메라 미리보기, **시작**을 누르면 로컬 QEV 추론, **종료·일어나기**를 누르면 추론/카메라 종료 및 기립입니다. 운영체제의 카메라 허용은 사용자가 직접 해야 합니다.
- 로그인/회원가입·PDF 퀴즈는 인터넷과 운영 중인 서버가 필요합니다. 모델은 설치 및 실행 시 인터넷이 필요 없습니다. 회원가입 이메일 확인 정책은 현재 서버 설정을 따릅니다.
- 배포본에는 계정 프로젝트의 공개 `AccountConnection.json`을 포함합니다. 공용 로비와 2인 퀴즈는 고정 서비스 주소로 접속합니다.

Windows 팀원은 [검토·실행 순서](WINDOWS_TEAM.md)를 먼저 확인하세요.

## 배포자 빌드

각 운영체제에서 해당 배포본을 만듭니다. 빌드 PC만 Unity **6000.4.5f1** 및 해당 Standalone Build Support, [uv](https://docs.astral.sh/uv/getting-started/installation/), Git과 인터넷이 필요합니다. 게임에는 빌드 도구가 필요 없습니다.

1. 통합 소스를 받습니다. 필요한 씬/팀 에셋도 포함되어 있어야 합니다.
2. `Assets/Account/Resources/AccountConnection.json`의 공개 계정 설정을 확인합니다. 깨끗한 체크아웃에서 그대로 2인 퀴즈 클라이언트를 빌드할 수 있습니다. Gemini API 키는 서버에서 관리합니다.
3. 해당 OS에서 실행합니다. Unity에서 이 프로젝트가 열려 있으면 먼저 저장하고 닫습니다.

macOS 터미널:
```sh
bash Tools/distribution/build-macos.sh
```
Windows PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File Tools/distribution/build-windows.ps1
```

기존 모델 캐시를 재사용하려면 Mac은 `--model-source /path/to/qev`, Windows는 `-ModelSource C:\path\to\qev`를 추가합니다. `--reuse-prepared`는 소스/의존성 변경이 없는 패키지 재검사에만 씁니다.

스크립트는 uv 관리 Python을 복사한 **전용 빌드 폴더**에 고정 버전 의존성을 설치하고 모델을 포함합니다. Python/모델 라이선스와 의존성 목록을 동봉합니다. 경로를 한글·공백 포함 경로로 옮겨 오프라인 실제 추론을 통과한 경우에만 ZIP/해시를 생성합니다. Mac/Windows 패키지는 서로 다르며 빌드 전 플랫폼·해시·연결 설정을 검사합니다.

- Mac 결과: `Builds/CampusCommons-macos-arm64/CampusCommons.app`
- Windows 결과: `Builds/CampusCommons-windows-x64/` **폴더 전체**. exe만 배포하지 않습니다.
- 자동 생성 모델/런타임, 빌드 결과는 Git에서 제외합니다. 완성 배포본은 GitHub Release 등의 다운로드 첨부 파일로 배포합니다. 일반 Git 커밋에 수 GB 파일을 올리지 않습니다.
- 공개 배포 전 macOS Developer ID 서명·공증과 Windows 서명/깨끗한 PC 실행 검사를 별도로 수행합니다. 이 저장소에 서명 인증서나 계정 암호를 넣지 않습니다. 현재 Mac에서 만든 로컬 검증본은 공증된 공개 배포본이 아닙니다.

## 준비·복구

런타임은 OS의 LocalApplicationData 아래 `CampusCommons/StudyRuntime/<패키지 SHA256>`에 저장합니다. 임시 폴더 추출이 성공한 뒤 준비 완료 표시를 만듭니다. 동시 실행은 파일 잠금으로 보호하고, 취소·손상 시 다음 실행에 재시도합니다. 웹캠 패널의 **공부 인식 다시 준비**는 현재 추론을 종료하고 동봉 패키지에서 복구합니다. 원본 게임 파일이 손상됐다면 게임 배포본을 다시 받습니다. 다른 게임 창이 추론 중이면 복구를 차단하고 해당 창을 종료하도록 안내합니다.

Intel Mac 및 Windows ARM은 현재 지원 대상으로 검증하지 않았습니다. Windows 네이티브 빌드/실행 결과와 macOS 검증 결과를 혼동하지 않도록 아래 기록을 확인합니다.

## 검증 기록

구현 및 실제 테스트 결과는 작업 인계 문서 `docs/tasks/cc-2.md`에 기록합니다.


## Windows CPU 패키지를 GitHub에서 준비하기

`.github/workflows/windows-runtime.yml`은 Windows runner에서 기존 `prepare_runtime.py`를 실행하고, 경로를 옮긴 뒤 네트워크 없이 모델을 실제 실행해 통과한 패키지만 artifact로 올립니다. QEV의 고정 소스가 CUDA index를 지정하므로, 같은 버전의 `torch/torchvision +cpu` wheel을 공식 CPU index에서 명시적으로 설치한 뒤 CUDA 미포함과 오프라인 추론을 다시 확인합니다. 게임·계정 설정·사용자 영상은 이 workflow에 올리지 않습니다. artifact 보관 기간은 3일입니다.

workflow가 main에 통합된 뒤 Actions → Windows study runtime → Run workflow로 실행합니다. 산출물은 런타임 패키지이며 게임 실행 파일 자체가 아닙니다. `manifest.json`과 `payload.zip`을 `Assets/StreamingAssets/StudyRuntime/`에 놓고 Unity Windows 빌드 메뉴를 실행합니다. `DistributionBuild`가 플랫폼·길이·SHA256을 검사합니다. Mac에서 Windows Mono 모듈로 빌드할 수 있지만 Windows의 GUI·카메라 검증을 대신하지 않습니다.

일반 빌드 스크립트는 QEV의 기본 의존성을 사용합니다. CPU 전용 배포를 재현할 때는 위 workflow의 검증된 artifact를 사용하거나 같은 wheel 교체·동결 목록 갱신·재검증 단계를 수행합니다. GPU 가속 배포와 CPU 배포의 용량·실행 성능을 구분합니다.
