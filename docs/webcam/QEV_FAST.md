# QEV-0.8B 속도 모드 수동 테스트

2026-11-12: 사용자가 Compact 웹캠 정확도가 낮다고 평가하여 기존 QEV-0.8B의
공식 fast 모드로 전환했다. 기존 모델 revision
`3933310d0637b5721c05633cb07b7de5fd11d050`과 로더
`ff55034b2f488e03fab774dc380b08d67a93071b`를 유지하고, 같은 revision의
`exit16.safetensors`를 추가했다. 네이티브 로더가 manifest SHA256을 검증한다.

- 공식 fast: 16층 조기 종료 + 전용 exit head, 이미지 상한 262144 pixels.
- 메모리 입력 경로도 공식 exit head를 사용한다. accurate head를 재사용하지 않는다.
- 기존 512px 긴 변 JPEG, 다섯 범주, 1초 시작 간격 목표와 프롬프트를 유지한다.
- 서버/API/화면에 실제 fast 모드를 표시한다. Compact는 여전히 accurate만 허용한다.
- 이미지 파일 저장·외부 전송 없음. 에이전트는 개인 웹캠을 사용하지 않았다.

## 실행

기존 0.8B 환경에서 `setup_qev.py`를 다시 실행하면 전용 head도 내려받는다.

```sh
/private/tmp/campus-qev/.venv/bin/python Tools/webcam/setup_qev.py --model-dir /private/tmp/campus-qev/model --cache-dir /private/tmp/campus-qev/hf-cache/hub
Tools/webcam/start_fast.command
```

현재는 서버가 실행 중이다. http://127.0.0.1:8770/ 새로고침 →
**QEV-0.8B · 속도 모드** 확인 → 카메라 시작.
모델을 전환할 때 이전 서버를 종료하고, `start_qev.command`(0.8B accurate),
`start_fast.command`(0.8B fast), `start_compact.command`(0.5B accurate) 중 하나를 실행한다.
세션 토큰이 바뀌므로 화면을 새로고침한다. 카메라 중지는 모델까지 내리지 않는다.
실행 터미널의 Ctrl+C는 모델 프로세스까지 종료한다.

## 실측과 한계

Apple M4 32GB, 공개 512×288 JPEG 5종×2 HTTP 요청:
첫 요청 0.914초, 이후 0.583–0.624초. macOS physical footprint 2.9GiB,
시작 포함 peak 3.5GiB. 기존 accurate steady 2.9GiB와 동일해서 이번 실측에서는
상주 메모리 절감은 확인하지 못했다. 게임·브라우저 메모리는 포함하지 않는다.

공개 5종 모두 기대한 범주와 일치했다. Compact가 other로 분류한 phone 예제도
phone으로 분류했다(선택 점수 약 0.651). **5종 예제는 웹캠 정확도 측정이 아니다.**
공식 파일 입력과 메모리 입력 점수는 5종 모두 동일했다.

Python 19 passed/4 opt-in skipped, Node 10 passed. Opt-in fast 계약/실제 모델
동등성 2 tests passed. 초기 미지원 상태에서 계약 검사 실패를 확인한 뒤 구현했다.
현재 Windows, Unity 동시 FPS, 개인 웹캠 품질은 미검증이다.
원본: [qev-m4-fast.json](results/qev-m4-fast.json).
