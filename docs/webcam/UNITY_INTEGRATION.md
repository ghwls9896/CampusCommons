# QEV fast 로비 웹캠 연동

사용자 확정(2026-11-12): QEV-0.8B fast만 유지. 로비 착석 후 시작 버튼으로 카메라를
켜고 공부중/자는중/자리비움/휴대폰 사용중/다른활동중을 표시한다. 일어서면 종료.
이미지/영상 저장 및 원격 전송/DB 없음. 자는중은 시각적 추정의 UI 이름이다.

설계: Assets/Webcam의 자동 부트스트랩이 기존 Seat.occupant와 Player.currentSpace를
읽는다. 공용 Player 및 씬 수정 없이 로비 착석일 때만 패널을 표시한다.
Unity WebCamTexture → 메모리 JPEG → 사용자 PC의 QEV fast 자식 프로세스.
시작 때만 권한 요청, 한 번에 추론 1개, 목표 시작 간격 1초. 자리 이탈/중지/포커스
상실/종료 시 카메라·요청·소유한 모델 프로세스를 종료한다. 재착석은 수동 시작.
오류/권한 거부는 행동 판정과 분리한다. 준비 중 상태를 임의로 공부로 기록하지 않는다.
런타임과 모델은 Git 밖의 사용자 홈 .campuscommons/qev에 설치하며
CAMPUS_QEV_HOME 환경변수로 경로를 바꿀 수 있다.

계획: fast 단일 런타임/문구 정리 → 독립 Unity 모듈 및 lifecycle 검사 → 공개 이미지
HTTP 회귀/Unity 컴파일·Play 검증 → 이전 테스트 런타임 삭제/잔여 파일 확인 → PR 인계.
정확한 저장소 Unity 6000.4.5f1은 미설치. 다른 버전 검증은 임시 복사본에서만 수행.

## 구현 및 현재 검증

- 독립 runtime bootstrap과 Seat.occupant 조회를 구현. 최신 main `0a6c3f7` 반영.
- 시작/중지 버튼 + F8, 부모 프로세스 사망 감시, 프레임 5초 이상 끊김 시 종료.
- 정상 판정은 다음 추론 중에도 유지하고 오류·취소·이탈 시 해제한다.
- 영구 runtime: 사용자 홈 `.campuscommons/qev`. 설치기 `install_runtime.py`.
- 개인 웹캠은 에이전트가 열지 않음. C# 프로세스 시작 → 공개 phone JPEG → 토큰 인증
  HTTP phone 판정 → 소유 프로세스 종료 PASS. Unity용 Mono의 standalone WebClient는
  MissingFieldException이 발생해 검증 harness만 raw TCP HTTP로 교체했다. 제품은 UnityWebRequest.
- 순수 세션 lifecycle 검사 PASS. 전체 프로젝트 C#은 Unity6000.6 참조 어셈블리와 Roslyn으로
  컴파일 PASS(기존 deprecated API와 JSON 역직렬화 필드 경고). Editor/Play 검증과는 구분한다.
- 6000.6 Editor batch는 라이선스 초기화, Hub 재실행은 OS -600 오류로 실패.
  사용자가 6000.4.5f1 추가 설치를 시작했으며 정확 버전 검사 대기 중.
- 독립 리뷰: 첫인칭 버튼 접근성과 영상 끊김 후 stale 상태를 지적하여 F8 및 timeout 추가.
  Unity는 score 키 집합을 독립적으로 완전 검증하지 않으며 Python에서 정확한 5개 키를 검증한다.

## 모델 정리

삭제 완료: 이전 웹캠 전용 SmolVLM/Qwen GGUF 및 projector·llama 실행기·테스트 환경,
QEV Compact 모델과 환경, 이전 QEV 임시 환경·설치 캐시, 중복 원본 safetensors.
다른 작업의 BAAI/Whisper/KcBERT/E5 캐시는 변경하지 않았다.
남긴 원본 backbone의 SHA256은 `4f4939ad2ad50a503fab21167e1bf2b6e9f465eeaea8b82304a099a55959055a`.
공통 backbone + loader용 head + 공식 fast exit16만 런타임에서 사용한다.

추가 확인: 이전 임시 폴더를 삭제한 상태에서도 `QEV_FAST_TEST=1` 공개 이미지 native
동등성 검사를 다시 수행한다. 설치 디렉터리는 약 2.3GiB(가중치/가상환경 포함, 디스크)
이며 실행 메모리 수치와 다르다. C# 프로세스 회귀 harness는
`Tools/webcam/tests/NativeProcessSmoke.cs`에 보관하고 `QEV_SMOKE_FIXTURES`로 공개 예제
폴더를 전달한다. private webcam 파일을 만들지 않는다.
