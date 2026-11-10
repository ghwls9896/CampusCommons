# QEV 수동 테스트 구현 검증 — 2026-11-10

후속으로 **졸음 의심(drowsy)**을 추가해 현재는 다섯 범주다.
다섯 공개 예시의 실제 HTTP 검사와 1초 간격 자원 측정은 [QEV_RESOURCES.md](QEV_RESOURCES.md)에 기록했다.
현재 MPS 로더의 메모리를 최적화해 실제 서버의 반복 점유가4.9→2.9 GiB, peak가4.9→3.8 GiB로 줄었다.
같은 공개 사진10회에서 기존 네이티브 점수와의 차이는0.0이었고 실제 메모리 회귀 검사도 통과했다.
화면 테스트는 추가 범주 표시를 포함해 9개가 통과했다. 아래 네 범주 수치는 변경 전 비교 기록이다.

실제 `ken-jo/QEV-0.8B`를 실행해 확인했다. **개인 웹캠의 분류 정확도는 아직 측정하지 않았다.**
공개 이미지 네 장을 반복한 결과를 정확도 백분율이나 사용자 환경 성능으로 해석하지 않는다.

## 고정한 모델 / 환경

| 항목 | 값 |
|---|---|
| 상류 소스 | [ken-jo/qev](https://github.com/ken-jo/qev/tree/ff55034b2f488e03fab774dc380b08d67a93071b), `ff55034b2f488e03fab774dc380b08d67a93071b` |
| 체크포인트 | [ken-jo/QEV-0.8B](https://huggingface.co/ken-jo/QEV-0.8B/tree/3933310d0637b5721c05633cb07b7de5fd11d050), `3933310d0637b5721c05633cb07b7de5fd11d050` |
| manifest의 backbone | 같은 저장소의 `4a245fab7757f2d0f82abb4df69fa294847c6f66` |
| backbone 파일 | 1,706,027,688 bytes; SHA256 `4f4939ad2ad50a503fab21167e1bf2b6e9f465eeaea8b82304a099a55959055a` |
| 판정 head | SHA256 `fd3877fc9bddc51ebd9f79cf9d42c43be11f1cf44a637e39fd55ca7a59be7cf8` |
| 실행 | accurate / 전체 24층 / early exit 끔 / 1 view / reasoning slots 4 / 학습된 head 및 calibration |
| 하드웨어 | Apple M4, 통합 메모리 32 GB, MPS |
| 소프트웨어 | Python 3.12.11, torch 2.10.0, transformers 5.17.0; 나머지는 requirements-qev.txt |
| 정밀도 | MPS backbone BF16, 학습된 scoring heads FP32; 양자화 없음 |

일반 `qev.load()`의 기본 모델을 사용하지 않고 `QEV.load()`로 위 로컬 체크포인트를 지정했다.
모델의 accurate 픽셀 한도 1,638,400은 유지한다. 라이브 카메라 입력의 긴 변은 512 px다.
상류 로더의 MPS 기본값 FP32에서는 첫 독서 이미지가 8.446초였으나, 원본 BF16 가중치 정밀도를 사용하면서 반복 처리 시간이 줄었다.
BF16 전환 후 첫 독서 추론은 2.485초였다. 모델 적재·첫 추론은 반복 추론보다 오래 걸린다.

## 메모리 입력의 네이티브 동등성

상류 `OptionModel.encode()`의 프롬프트·condition readout·학습 head·calibration 경로를 따르며 PIL 이미지를 직접 전달한다.
공개 독서 이미지 한 장에서 상류 파일 입력 `predict()`와 메모리 입력 어댑터를 비교했다.
MPS FP32에서 네 확률의 **최대 차이는 0.0**, 선택은 둘 다 공부중(0.936885)이었다.
이 검사는 입력 전달 경로를 검증한 것이며, BF16이 FP32와 비트 단위로 같다는 주장은 아니다.

## 실제 HTTP 경로 결과

최종 프롬프트, BF16 서버, 공개 사진을 512×288 검은 여백 포함 JPEG(quality 85)로 만들어 사용했다.
UI와 동일한 `POST /api/infer`에 각 사진을 두 번 전송했다.
표의 시간은 서버의 이미지 디코딩+실제 모델 추론이며, 웹캠 촬영이나 UI 렌더링 시간은 포함하지 않는다.

| 공개 예시 | 기대 / 최종 선택 | 선택 점수 | 첫 번째 / 두 번째 분석 시간 |
|---|---|---|---|
| 독서 | studying / studying | 0.9301 | 1.175 / 0.803초 |
| 휴대폰 조작 | phone / phone | 0.9114 | 0.779 / 0.733초 |
| 기타 연주 | other / other | 0.9421 | 0.746 / 0.749초 |
| 사람 없이 고양이만 있음 | away / away | 0.8991 | 0.786 / 0.776초 |

[원시 측정값 및 입력 SHA256](results/qev-m4-public.json).
초기 프롬프트는 사람이 책상에 있다고 미리 설명하여 마지막 예시를 공부중(0.6652)으로 오판했다.
이를 실제 회귀 테스트에서 확인한 뒤, 사람의 존재를 가정하지 않도록 맥락과 기준을 수정했다.
같은 실제 모델 테스트가 실패에서 통과로 바뀌었다. 이 한 장의 수정으로 일반적인 자리 비움 정확도가 입증되지는 않는다.

## 공개 이미지 출처

- 독서 `reading_001.jpg`, 휴대폰 `texting_message_001.jpg`, 기타 연주 `playing_guitar_001.jpg`:
  Stanford40의 공개 예시, [Hugging Face 미러](https://huggingface.co/datasets/zrchen03/Stanford40_Dataset/tree/5ba578733e5070d4407ef804dcfa370a6f1edba6), revision `5ba578733e5070d4407ef804dcfa370a6f1edba6`의 `Stanford40.zip/JPEGImages/`.
- 사람 없음: [Hugging Face documentation-images/coco_sample.png](https://huggingface.co/datasets/huggingface/documentation-images/blob/main/coco_sample.png)의 고양이 두 마리 사진.
- 사진을 눈으로 확인해 기대 범주를 정했다. 학습 데이터와의 중복은 확인하지 않았다. 이미지·모델·캐시·개인 촬영물을 저장소에 포함하지 않았다.

## 실행한 검사 / 남은 검증

- Python 도구 테스트 16개 통과, 선택 실제 모델 테스트 1개는 일반 실행에서 생략.
- Node 모의 카메라 테스트 8개 통과: 자동 카메라 접근 없음, 1초 시작 간격, 느린 추론의 큐 방지, 중지/권한 지연/늦은 응답/탭 이동, 다음 분석 중 현재 결과 평가, 오류 시 평가 해제.
- 선택 실제 모델 HTTP 테스트 1개에서 네 공개 예시를 모두 확인.
- 팀 도구 테스트 16개 통과. 제출 커밋의 팀 범위 검사 통과; GitHub `team-checks`도 성공.
- Chrome 화면에서 accurate/MPS/네 범주/시작 버튼/카메라 OFF를 확인. 실제 카메라 권한을 요청하거나 사용자 영상을 보지 않았다.

독립 최종 코드 리뷰에서 다음 촬영 시작 시 평가 버튼이 즉시 잠겨 느린 추론에서 평가가 어려운 문제 1건을 찾았다.
회귀 테스트의 실패를 확인하고, 화면에 보이는 결과는 다음 분석 중에도 한 번 평가할 수 있도록 수정한 뒤 8개 테스트가 통과했다.
별도 지적된 Critical/Minor 항목은 없었다. 개인 웹캠 품질과 Windows/Unity/시간 누적은 기존 범위대로 후속 검증으로 남겼다.

실제 웹캠·조명·책상 구도·화면 밖 휴대폰·컴퓨터 공부와 게임 구별은 사용자가 직접 검증해야 한다.
Windows 실행, Unity 로비 착석/기립 연결, Unity FPS 및 공부 시간 누적은 이번 구현에서 검증하지 않았다.
