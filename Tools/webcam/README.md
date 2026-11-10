# QEV-0.8B 로컬 웹캠 테스트

`ken-jo/QEV-0.8B`의 **accurate 모드, 전체 24층, 기본 학습된 판정 헤드**를 사용한다.
생성형 답변을 파싱하거나 Fast/early-exit 모드로 바꾸지 않는다.
Unity 로비 연결 전, 본인 웹캠으로 분류 품질을 확인하는 독립 도구다.

## 현재 PC에서 바로 테스트

모델과 Python 환경을 준비해 두었다. 서버 실행 중에는
**http://127.0.0.1:8770** 에서 `카메라 시작`을 누르고 권한을 허용한다.
손·책·노트·휴대폰이 보이도록 조정한 뒤 네 상황을 각각 시험한다.

| 표시 | 코드 | 기준 |
|---|---|---|
| 공부중 | `studying` | 독서, 필기, 컴퓨터 작업이 보임 |
| 휴대폰 사용중 | `phone` | 손에 든 휴대폰을 보거나 조작하거나 통화함; 책상 위 미사용 폰은 제외 |
| 다른 활동중 | `other` | 사람이 있고 식사·놀이 등 다른 활동이 보임 |
| 자리 비움 | `away` | 화면에 사람이 없음 |

매 판정에 사용한 사진, 네 선택 점수, 분석 시간과 실제 촬영 간격을 표시한다.
`맞음` / `틀림`은 현재 화면에서만 집계하며 새로고침하면 초기화된다.
점수는 실제 정확도가 아니며, 집중 여부나 화면 밖 행동은 알 수 없다.
컴퓨터 작업은 게임·영상 시청과 구분할 수 없으므로 공부중으로 오판할 수 있다.
애매한 장면도 요청대로 네 범주 중 하나를 선택한다. 실행 실패는 활동이 아닌 `분석 오류`로 표시한다.

`중지`, 다른 탭으로 이동, 페이지 닫기는 카메라 트랙을 해제한다.
진행 중이던 모델 계산은 끝날 수 있지만 중지 뒤 결과는 화면에 반영하지 않는다.
카메라 중지는 모델 프로세스 종료와 별개다. 모델까지 종료하려면 실행 터미널에서 **Ctrl+C**를 누른다.

## 실행 환경 설치 / 재시작

검증 환경은 macOS / Apple M4 / 통합 메모리 32 GB / Python **3.12**다.
Windows, 다른 GPU, CPU 전용 속도는 미검증이다. 네이티브 QEV 소스가 Python 3.12를 요구한다.
일회성 다운로드에 네트워크가 필요하며 모델 가중치는 약 **1.71 GB**, Python 라이브러리 용량은 별도다.

저장소 루트에서 실행한다. `uv`가 설치되어 있어야 한다.
모델·환경은 Git 밖에 두며, 아래 기본 경로는 현재 PC의 준비된 환경과 같다.
`/private/tmp`가 정리되면 다시 설치해야 한다. 지속 보관하려면 처음부터 별도 경로를 지정한다.

```sh
export QEV_WORK_DIR="${QEV_WORK_DIR:-/private/tmp/campus-qev}"
uv venv --python 3.12 "$QEV_WORK_DIR/.venv"
uv pip install --python "$QEV_WORK_DIR/.venv/bin/python" -r Tools/webcam/requirements-qev.txt
"$QEV_WORK_DIR/.venv/bin/python" Tools/webcam/setup_qev.py \
  --model-dir "$QEV_WORK_DIR/model" --cache-dir "$QEV_WORK_DIR/hf-cache/hub"
```

설치 후 재시작에는 설치 명령을 반복하지 않는다. macOS에서는
`Tools/webcam/start_qev.command`를 실행하거나 다음 명령을 쓴다.

```sh
export QEV_WORK_DIR="${QEV_WORK_DIR:-/private/tmp/campus-qev}"
PYTORCH_ENABLE_MPS_FALLBACK=1 "$QEV_WORK_DIR/.venv/bin/python" Tools/webcam/live.py \
  --model-dir "$QEV_WORK_DIR/model" --cache-dir "$QEV_WORK_DIR/hf-cache/hub" --port 8770
```

`Model ready` 이후 브라우저에서 위 주소를 연다. `localhost` 대신 **127.0.0.1**을 사용한다.
포트를 바꾸면 브라우저 주소도 바꾼다. 카메라를 시작하려면 테스트 탭을 보이는 상태로 둔다.
권한이 막혔으면 브라우저의 이 사이트 카메라 권한과 macOS 카메라 권한을 확인한다.

## 주기 / 개인정보

- 촬영 시작 시점 기준 **1초 간격 목표**, 동시에 모델 요청은 하나만 처리한다.
- 추론이 1초를 넘으면 요청을 쌓지 않고 완료 직후 새 프레임을 촬영한다. 따라서 모든 PC에서 1 Hz를 보장하지 않는다.
- 미리보기는 최대 1280×720을 요청하고, 분석 사진은 비율을 유지해 **긴 변 512 px**로 축소한다. 정확도 우선 모델의 최대 픽셀 예산 1,638,400은 유지하되, 실제 입력은 그보다 작다. 작은 글씨·멀리 있는 폰은 놓칠 수 있다.
- MPS에서는 원래 가중치 정밀도인 BF16으로 backbone을 실행하고 판정 헤드는 FP32를 유지한다. 양자화나 조기 종료는 사용하지 않는다.
- 브라우저 → **127.0.0.1의 로컬 Python 프로세스** → 모델 순서다. 원격 서버·DB 없이 동작하며 JPEG와 결과는 메모리에서만 처리한다. 사진·동영상·평가를 파일에 기록하지 않는다.
- 모델 준비 후 실행은 Hugging Face 오프라인 모드다. 외부 API나 분석 서비스, 원격 이미지 링크를 사용하지 않는다.

## 검증

[실제 모델 측정 기록](../../docs/webcam/QEV_TEST_RESULTS.md)을 참고한다.
공개 예시 네 장은 회귀 점검용이며 웹캠 정확도 측정값이 아니다.

```sh
python3 -m unittest discover -s Tools/webcam/tests -v
node --test Tools/webcam/tests/test_app.mjs
python3 -m unittest discover -s Tools/team/tests -v
python3 Tools/team/check.py --base origin/main
```

일반 테스트는 GPU와 카메라가 필요 없다. HTTP 전송 테스트의 모델은 대역이며, JS 테스트도 모의 카메라를 사용한다.
실제 QEV 서버를 켜고 공개 JPEG 네 장을 따로 준비한 경우 다음 선택 검사를 실행할 수 있다.
`FIXTURE_DIR`에는 `reading.jpg`, `phone.jpg`, `other.jpg`, `away.jpg`가 필요하다.

```sh
QEV_SMOKE_FIXTURES="$FIXTURE_DIR" python3 -m unittest discover -s Tools/webcam/tests -p test_qev_runtime.py -v
```

`probe.py`와 관련 테스트는 이전 llama.cpp 후보 조사 도구다. 라이브 QEV 실행 경로에서는 사용하지 않으며, 이전 도구의 `unknown`은 이 화면의 범주가 아니다.
상류 Apache-2.0 라이선스와 메모리 입력 어댑터 출처는 `QEV-LICENSE.txt`, `QEV-NOTICE.txt`에 있다.
