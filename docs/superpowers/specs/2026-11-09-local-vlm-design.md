# QEV local webcam test before Unity integration

## User-approved update, 2026-11-10

The user selected **ken-jo/QEV-0.8B**, accurate mode, a **1,000 ms start-to-start
inference interval**, and exactly four activity labels: studying, phone, other,
away. There is no unknown activity category. Inference/camera failures are errors,
not activity observations. This explicit request supersedes the earlier
public-image-only feasibility scope below.

The browser requests camera access only after a Start click. Frames travel only
to a numeric loopback listener, stay in memory, and are never written to disk,
uploaded or stored in a database. Stop, page exit and backgrounding release camera
tracks; an old response cannot change a restarted/stopped session. Only one
inference is in flight. Slow inference drops scheduled captures and uses the next
fresh frame; there is no backlog or guarantee of 1 Hz throughput before measurement.
The screen shows actual latency and the image that was judged.

Use the pinned native QEV model, trained head, calibration and all 24 layers.
Do not substitute Qwen base weights, the similarly named labqoat model, Fast mode
or generated JSON. The in-memory adapter must agree with upstream file-based
inference on a public fixture. The test UI shows raw per-frame choices for direct
evaluation; production study timers/debouncing remain outside this phase.

Unity seating integration remains a later step. No shared Unity files change.

## Earlier feasibility context

The user changed the approved MediaPipe seating prototype to a small VLM and explicitly asked to verify execution conditions first. This phase measures feasibility; it does not claim the Unity seating feature is implemented.

## Intent and constraints
- Eventually: sitting in a lobby chair starts local webcam inference; standing stops it. No cloud inference or database.
- First: evaluate real small vision-language models on public, non-personal images, with reproducible prompts, raw responses, timing, model revisions and memory observations.
- Distinguish observable reading/writing, phone use, no person, and unknown. Do not infer comprehension, motivation, identity, or actual mental attention.
- Avoid installing a global LLM service. Use a pinned portable runtime in temporary local storage. A loopback-only inference process is permissible for measurement; it is not a remote service and must not accept public connections.
- Keep downloaded model weights, runtime binaries and source images outside Git. No personal camera recording, upload, or live capture during this phase.
- Preserve Unity 6000.4.5f1, all scenes, Player/GameStart and existing quiz work. Independent branch codex/webcam-study, owner HIGHCHOI, issue #7.

## Approaches
The earlier MediaPipe proposal gives landmarks/presence but does not identify study activity. The user selected a small image-capable VLM instead. The chosen feasibility approach is portable llama.cpp with quantized weights; starting with SmolVLM2-500M and evaluating a second small candidate if available. A Python/Transformers stack is an alternative but adds distribution dependencies. Neither model is assumed accurate before testing.

## Acceptance for this phase
A local inference result and measured latency/memory, or a precisely evidenced runtime blocker; a small public-image sanity check with explicit limits rather than an invented accuracy guarantee; a reproducible test tool; clear next-stage PC/camera requirements and unresolved Windows/Unity measurements. Successful model loading alone does not pass the activity-recognition requirement. If results are unreliable, report that and defer product integration rather than shipping misleading study labels.
