# cc-7 handoff

- Branch: codex/webcam-study, owner HIGHCHOI, issue #7.
- Base: origin/main fc5d8c7.
- User changed MediaPipe presence detection to small VLM and requested execution-condition verification first.
- Existing quiz PR #4 is independent; Player/GameStart/scenes remain unchanged.
- Native worktree creation rejected the nested repository sourceRoot; used the repository-documented sibling Git worktree instead. Original checkout untouched.
- Baseline: Tools/team unittest 16 passed.
- Latest implementation commit: eb95e87 (reduces MPS memory while preserving native scores); documentation follows on the same PR.
- Delivered phase: native ken-jo/QEV-0.8B accurate, full 24 layers, five choices including suspected drowsiness, 1-second start-to-start target, local manual webcam UI.
- Entry point: Tools/webcam/README.md. Server URL http://127.0.0.1:8770; camera starts only on the user's click. Stop/tab hiding releases tracks; Ctrl+C stops the model process.
- Validation: Python 16 pass + 2 opt-in skips; Node 9 pass; team tests 16 pass; both real QEV public-image HTTP and MPS memory tests pass when opted in. Memory test observed RED at4,528,062,464 driver bytes, then GREEN with identical pinned public probabilities. Native public-image file/memory score difference0.0 was checked during initial implementation. Current team scope check passes; latest GitHub CI is separate. Details and limitations: docs/webcam/QEV_TEST_RESULTS.md and QEV_RESOURCES.md.
- The initial four-state implementation received an independent final review; feedback-lifecycle defect fixed in 53e5f87. This small five-state follow-up was checked with RED→GREEN backend/UI tests, actual model/public fixtures and local diff review.
- Current M4 live-server inference:0.763–0.798s at512×288, BF16 backbone/FP32 heads; footprint2.9GiB/peak3.8GiB (before optimization4.9GiB/peak4.9GiB). Public5×2 native score max difference0.0. CPU25% of one core was measured before this memory optimization. GPU utilization percentage and power unmeasured. Model weights1.71GB; experimental directory size includes duplicate caches and is separate from RAM. Startup and other hardware can be slower; no queued frames.
- MPS uses native CPU loading/validation, BF16 cast before MPS transfer, then one startup empty_cache. No upstream monkey-patch, quantization, resolution/layer/interval/category changes or per-frame cache flushing. Native CPU/CUDA paths unchanged. Direct BF16 experiment2.3GiB was not deployed; safe native API path2.9GiB is running at the same URL and old tabs need refresh after restart.
- Remaining: user's own webcam quality check, Windows/device performance, Unity seating/camera lifecycle integration and study-time recording. Public fixtures are not a webcam accuracy benchmark.
- PR: https://github.com/ghwls9896/CampusCommons/pull/9. Final human review: ghwls9896; no merge or self-approval performed.
- No shared files claimed or modified.

## 2026-11-12 Compact 비교 추가

- 사용자 요청으로 QEV-0.5B (이전 이름 QEV-0.8B-compact) 다운로드 및 별도 환경 실행.
- 기존 0.8B 환경/실행기는 유지. Compact는 `start_compact.command`, 16층 accurate,
  1초 목표, 기존 5범주. 현재 테스트 서버는 Compact로 실행한다.
- 공개 5종 중 4종 기대 범주 일치; phone→other 오분류를 숨기지 않고 기록.
  공식 파일 입력/메모리 입력 확률은 5종 모두 동일. 개인 카메라는 사용하지 않음.
- M4 HTTP 반복 추론 0.588–0.609초, 첫 요청 0.873초; footprint 1.7GiB, peak 2.1GiB.
- Python 18 pass/3 skip, Node 9 pass, team 16 pass; Compact 실제 계약/동등성 3 pass.
- 재현/측정 한계: docs/webcam/QEV_COMPACT.md. Unity FPS 및 개인 웹캠 정확도 미검증.
- 변경은 기존 cc-7 범위 안에만 있으며 공유 파일 조율 대상 없음. 같은 PR #9로 인계.

## 2026-11-12 0.8B fast 전환

사용자가 Compact 정확도가 낮다고 평가하여 공식 0.8B fast로 전환 요청.
같은 고정 revision의 exit16 head 추가, 공식 조기 종료/전용 head로 메모리 추론.
현재 서버 QEV-0.8B fast, 1초 목표/5범주/프레임 로컬 RAM 처리 유지.
공개 5종×2 모두 기대 범주 일치; 공식 파일 입력과 메모리 입력 확률도 동일.
개인 카메라 접근 없음. 첫 HTTP 0.914s, 이후 0.583–0.624s, footprint 2.9GiB,
peak 3.5GiB. 정확도 모드 대비 상주 메모리 절감은 실측되지 않음.
Python 19 pass/4 skip, Node 10 pass, opt-in fast 2 pass.
가이드 docs/webcam/QEV_FAST.md 및 원본 JSON 참고. 같은 cc-7 범위, 공유 파일 변경 없음.

## 착석 미리보기 / 종료 기립 후속

- 사용자의 후속 요청에 따라 시작 전에는 미리보기만 표시, 종료 버튼은 기립까지 수행.
- Unity6000.4.5f1 누락 imageconversion 모듈 수정, 실제 카메라→로컬 모델→정리 검증 통과.
- Packages 두 파일은 새 공통 파일 변경이며 최종 사람 리뷰 대상. 공용 Player와 씬 수정 없음.
- 상세 검증/남은 한계는 docs/webcam/UNITY_INTEGRATION.md 마지막 절.
- 통합 실행본은 별도 로컬 브랜치이며 main 병합과 구분. 최종 사람 승인 ghwls9896.
