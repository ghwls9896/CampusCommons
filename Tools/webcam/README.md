# QEV-0.8B fast 로컬 공부 인식

확정 모델: `ken-jo/QEV-0.8B`, revision `3933310d0637b5721c05633cb07b7de5fd11d050`.
공식 fast(16층 조기 종료 + 전용 exit16 head)만 실행한다. Compact/이전 VLM 실행기는 제거했다.

## Unity에서 사용

팀 Unity 버전은 **6000.4.5f1**이다. `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity`를
열고 로그인 → 로비 좌석 근처에서 E → 카메라 미리보기 → **시작**을 누른다.
**종료 · 일어나기**를 누르면 분석과 카메라를 끄고 좌석에서 일어난다.
F8은 시작/분석 중 종료 단축키다. E로 직접 일어나도 정리한다.

표시: **공부중 / 자는중 / 자리비움 / 휴대폰 사용중 / 다른활동중**.
`자는중`은 졸음·휴식처럼 보이는 모습의 추정이며 실제 수면을 확정하지 않는다.
착석 시 카메라 권한을 요청하며 미리보기만 켜고, 시작 전에는 모델을 실행하거나
프레임을 분석하지 않는다. 포커스 상실 시 카메라·분석을 중단하며 다시 돌아오면
미리보기만 재개한다. 분석 재개에는 시작 버튼을 누른다. 권한 거부/카메라 오류는
재시도 버튼으로 다시 시도할 수 있다. 로그아웃 시 좌석 점유도 해제한다.

- 카메라에서 메모리 JPEG(긴 변 512px, 품질85)를 만들어 PC 내부 루프백에 전달한다.
- 목표 시작 간격1초, 한 번에 요청1개, 느릴 때 프레임 대기열을 만들지 않는다.
- 이미지/영상/관측 결과를 파일·DB에 저장하거나 외부에 전송하지 않는다.
- 모델 준비/권한 거부/카메라 끊김/분석 실패는 행동 범주와 다른 오류로 표시한다.
- 현재 공부 시간 누적·DB 연동은 하지 않는다.

## 개발용 수동 설치

완성 게임은 Python·QEV 모델을 동봉하고 처음 실행에 자동 준비합니다. 아래 설치는 개발용 선택 경로입니다. Windows 팀원의 검토·빌드는 [Windows 팀원 안내](../../docs/distribution/WINDOWS_TEAM.md)를 따르세요.

Python 3.12 및 Git을 준비한 후 저장소 루트에서:

```sh
python3.12 Tools/webcam/install_runtime.py
```

기본 설치 경로는 사용자 홈의 `.campuscommons/qev`이다. 모델, Python 가상환경,
작은 로컬 앱을 함께 설치하며 Git에는 들어가지 않는다. 다른 경로를 쓰려면 설치 시
`--runtime-dir 경로`를 주고 Unity 실행 환경의 `CAMPUS_QEV_HOME`에도 같은 경로를 지정한다.
Windows에서는 해당 기기에 맞는 PyTorch/CUDA 지원을 별도 확인해야 한다.
설치 후에는 오프라인 실행됩니다. 배포 빌드는 이 개발용 가상환경에 의존하지 않고 별도의 동봉 패키지를 만듭니다. [데스크톱 배포 안내](../../docs/distribution/DESKTOP.md)를 참고하세요. Windows 실제 실행과 공개 배포용 서명·공증은 아직 검증/진행이 필요합니다.

현재 Mac에는 설치 완료. 코드 수정 후에는 설치를 다시 실행해 app 파일을 갱신한다.
QEV fast에도 원본 공통 backbone과 loader용 head가 필요하므로 이 파일들은 삭제하지 않는다.

## 브라우저 단독 점검

`Tools/webcam/start_fast.command` 실행 후 http://127.0.0.1:8770/ .
`start_qev.command`도 동일한 fast 전용 실행기의 호환 이름이다.
Unity는 무작위 루프백 포트의 별도 자식 프로세스를 시작한다. 브라우저 점검 서버와
동시에 실행하면 모델 메모리가 중복되므로 단독 점검 종료 후 Unity를 실행한다.

## 검증

```sh
python -m unittest discover -s Tools/webcam/tests -v
node --test Tools/webcam/tests/test_app.mjs
```

`Assets/Webcam/Editor/WebcamLobbyVerification.cs`는 실제 로비에서 착석/수동 분석 시작 대기/
종료와 계정 차단 시 좌석 해제를 검사하며 카메라를 열지 않는다. `StudySessionChecks`는 취소 후 늦은
응답, 재착석, 포커스 상실, 영상 끊김과 한국어 상태를 검사한다.

[Unity 통합 기록](../../docs/webcam/UNITY_INTEGRATION.md) 및 [fast 실측](../../docs/webcam/QEV_FAST.md).
이전 Compact/accurate 결과 문서는 비교 이력이며 현재 사용 가능한 모델/실행기를 뜻하지 않는다.
