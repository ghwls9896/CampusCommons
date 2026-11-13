# QEV-0.5B Compact 수동 웹캠 테스트

2026-11-12 사용자 요청에 따라 기존 QEV-0.8B와 별도 환경에서 비교한다.
모델은 `ken-jo/QEV-0.5B` (`QEV-0.8B-compact`의 새 이름), revision
`0d99aa34b354a81a4b963cb3059b75466174fd54`로 고정한다. 공식 소스는
`e091f7ae8c6f064860d59be560faa27f0387ff35`이다.

16층 전체를 사용하는 accurate 모드이며 fast 모드는 없다. BF16 backbone,
FP32 판정 head를 사용한다. 입력/범주는 기존과 동일하다: 512px 긴 변 JPEG,
1초 시작 간격 목표, 공부/폰/다른 활동/자리 비움/졸음 의심. 이전 추론이 길어지면
다음 최신 프레임을 처리하며 요청을 쌓지 않는다. 프레임은 메모리에서 처리하며
파일/DB에 저장하지 않는다. 카메라 시작은 사용자 버튼으로만 수행한다.

## 설치와 실행

Python 3.12 가상환경을 별도로 만들고 저장소 루트에서 실행한다.

```sh
python3.12 -m venv /private/tmp/campus-qev-compact/.venv
/private/tmp/campus-qev-compact/.venv/bin/python -m pip install -r Tools/webcam/requirements-compact.txt
/private/tmp/campus-qev-compact/.venv/bin/python Tools/webcam/setup_compact.py --model-dir /private/tmp/campus-qev-compact/model
Tools/webcam/start_compact.command
```

`http://127.0.0.1:8770/`을 새로고침하고 모델 이름이 **QEV-0.5B Compact**인지
확인한 뒤 카메라 시작을 누른다. 모델 전환 후에는 세션 토큰이 바뀌므로 새로고침한다.
카메라 중지는 촬영을 종료한다. 모델 메모리까지 해제하려면 실행 터미널에서 Ctrl+C.
같은 포트에 두 모델을 동시에 실행하지 않는다.

기존 모델을 비교하려면 Compact 프로세스를 종료하고 `Tools/webcam/start_qev.command`를
실행한다. 기존 모델 파일과 가상환경은 유지된다. `QEV_COMPACT_DIR`로 별도의 영구
설치 위치를 지정할 수 있다. 기본 `/private/tmp`는 OS에 의해 삭제될 수 있다.

## 비교 방법과 한계

같은 구도/조명에서 책 읽기, 필기, 컴퓨터 보기, 휴대폰 사용, 자리 비움,
고개를 숙여 읽기, 눈을 감거나 책상에 엎드린 모습을 각각 확인한다.
공개 예제 통과는 개인 웹캠 정확도를 보장하지 않는다. 모델 축소뿐 아니라 기반
가중치 버전도 달라 기존 모델과 점수 자체가 동일해야 하는 비교는 아니다.
Unity 동시 실행 FPS, Windows/CUDA 성능은 별도 검증 대상이다.

공식 자료: https://huggingface.co/ken-jo/QEV-0.5B

## 이 Mac에서 확인한 결과

Apple M4 / 32GB, 512×288 공개 JPEG 5종을 두 번씩 HTTP 요청했다.
첫 추론 0.873초, 이후 0.588–0.609초. 서버의 macOS physical footprint는
1.7GiB, 시작 포함 peak 2.1GiB였다. 이전 0.8B 측정은 2.9/3.8GiB였다.
실행 시점/부하가 다른 측정이며 게임·브라우저 메모리는 포함하지 않는다.

공개 예제는 5종 중 4종이 기대 범주와 일치했다. **phone 예제는 other로
오분류**했다(phone 0.394, other 0.435). 기존 0.8B는 이 예제를 phone으로
분류했다. 기존 프롬프트는 변경하지 않았다. 정확도가 동일하다고 주장하지 않는다.
공식 파일 입력과 메모리 입력은 5종 모두 확률까지 동일했다.

검사: Python 일반 18 passed/3 opt-in skipped, Node 9 passed, 팀 도구 16 passed.
Compact opt-in 실제 모델 3 tests passed(계약 2 + 공식 입력 동등성 1).
이 동등성 검사는 분류 정확도 검사가 아니다. 처음 제한된 프로세스에서는
MPS 접근 오류가 났고, GPU 접근이 가능한 실행에서 위 결과를 확인했다.
원본 수치: [qev-m4-compact.json](results/qev-m4-compact.json).
