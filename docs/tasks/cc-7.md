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
