# QEV-0.8B 자원 실측 — 2026-11-10

Apple M4 / 통합 메모리 32 GB에서 현재 로컬 테스트 프로그램을 측정했다.
accurate, 24층, BF16 backbone/FP32 heads, 긴 변 512px 입력을 유지했다.
졸음 의심을 추가해도 같은 가중치와 한 번의 forward를 사용한다. 새 모델 다운로드는 없다.
선택지가 늘면서 프롬프트 길이·선택 점수는 바뀌므로 정확도·부하가 완전히 같다고 보장하지 않는다.

## 메모리 / 저장 공간

| 측정 대상 | 결과 / 의미 |
|---|---|
| 모델 backbone 가중치 | 1,706,027,688 bytes = 약 1.71 GB (1.59 GiB); 판정 head는 약 0.36 MB 추가 |
| 실행 중 프로세스 footprint | 다섯 범주를 반복 처리한 뒤 약 **4.9 GiB**, 해당 프로세스 peak도 약 **4.9 GiB** (`vmmap -summary`) |
| 재시작 전 기존 네 범주 프로세스 | 오랜 유휴 상태에서 footprint 4.7 GiB, peak 4.9 GiB. CPU RSS만 보면 약 174 MiB여서 전체 점유를 설명하지 못함 |
| 설치된 Python 가상환경 | `du -sk`: 758,296 KiB, 약 0.78 GB. 이 PC에서 설치한 라이브러리 기준 |
| 테스트용 모델 폴더 / HF 캐시 | 각각 약 1.74 GB. 앞선 검증 중 같은 가중치를 두 위치에 보관한 상태이며 배포 필수 중복은 아님 |
| 현재 QEV 실험 폴더 전체 | `du -sk` 4,894,420 KiB = 약 **5.01 GB**. 중복 가중치·라이브러리·설치 캐시·공개 테스트 자료 포함, 배포본 크기가 아님 |

GB는 10억 bytes, GiB는 2³⁰ bytes다. 프로세스 footprint는 가중치 파일 크기와 다르며,
계산용 버퍼·할당 캐시 등도 영향을 준다. 유휴 상태에서 압축/페이지 아웃된 메모리도
있으므로 footprint를 그 순간의 RSS나 독립적인 VRAM 요구량으로 바꾸어 부르지 않는다.
모델을 종료해야 프로세스 점유를 반납한다. 화면의 카메라 중지는 모델 종료와 별개다.

## 연산 부하

공개 사진 다섯 장을 두 번씩, 촬영 시작 시점 기준 1초 간격 목표로 HTTP 전송했다.
모델 준비와 첫 추론은 먼저 끝낸 상태다. 사용자 카메라·Unity·브라우저 영상 처리 부하는 포함하지 않는다.

| 항목 | 결과 |
|---|---|
| 측정 구간 | 10.045초, 실제 모델 요청 10회 |
| 한 번의 모델 분석 | **0.792–0.808초** (JPEG 디코딩 포함, 준비된 모델) |
| Python 프로세스 CPU 시간 증가 | **2.51초** (`ps -o time=` 전후 차이) |
| CPU 평균 | **25.0%**, 100%를 CPU 코어 하나로 계산한 값. 전체 CPU의 25%라는 의미는 아님 |
| 가속 장치 | MPS (M4 GPU) |
| GPU 사용률·소비 전력 | 별도 측정하지 않음. 분석 시간/1초 비율을 GPU 사용률로 쓰지 않음 |

[원시 결과](results/qev-m4-five-class-resources.json).
macOS의 GPU 메모리를 따로 계측할 때는 tensor 점유와 Metal driver 점유를 구분해야 한다.
PyTorch의 [driver_allocated_memory 문서](https://docs.pytorch.org/docs/2.10/generated/torch.mps.driver_allocated_memory.html)는 driver 값에 MPS 할당 캐시와 framework 할당도 포함한다고 설명한다.
이번 수치는 그 API의 값이 아니라 `vmmap`의 프로세스 footprint다.

다른 PC의 최소 RAM·GPU 사양이나 Unity 동시 실행의 FPS는 아직 측정하지 않았다.
이 결과로 CPU 전용 PC나 8 GB PC에서 동일한 1초 주기를 보장할 수는 없다.

## 다섯 번째 공개 예시

추가 이미지: D. Sharon Pruitt / Pink Sherbet Photography,
[Free College Pathology Student Sleeping Creative Commons (6961676525)](https://commons.wikimedia.org/wiki/File:Free_College_Pathology_Student_Sleeping_Creative_Commons_(6961676525).jpg),
[CC BY 2.0](https://creativecommons.org/licenses/by/2.0/). 공개 960px 미리보기를 비율 유지 축소하고
검은 여백을 넣어 512×288 JPEG(quality 85)로 변환했다. 원본/변환 이미지는 Git에 넣지 않았다.

책상에 머리를 댄 모습이 보이는 이 예시는 `drowsy`(약 0.8097)로 분류됐다.
기존 독서·휴대폰·기타 연주·사람 없음 예시도 각 기대 범주를 유지했다.
사진 다섯 장의 회귀 점검이며 졸음 검출 정확도나 실제 수면 검증은 아니다.
