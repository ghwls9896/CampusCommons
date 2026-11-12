# Desktop Distribution Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task.

**Goal:** Package all client connection settings and an automatically prepared private Python/QEV runtime in desktop game downloads.
**Architecture:** Native build-time payload preparation; pure C# safe installer and Unity readiness UI; validated per-platform builds.
**Tech Stack:** Unity6000.4.5f1, C#/.NET, Python3.12.11, uv managed Python, pinned QEV.
**Spec:** docs/superpowers/specs/2026-11-12-desktop-distribution-design.md

## Global Constraints
- Windows x64 / macOS ARM64. No system Python/admin dependency for players.
- Local secrets remain ignored; current team demo quiz token is bundled only in binaries.
- Preserve existing account/quiz/webcam gameplay and Sunbox files. No main push or deployment changes.

## Review Focus
- Interrupted/corrupt/hostile ZIP and concurrent first launches never produce a ready partial runtime.
- Wrong-platform package and missing private config must fail before shipping.
- Fresh machine cannot accidentally use developer Python/model paths or environment variables.
- Preparation UI must remain responsive; failures/cancel/retry do not prevent login/quiz.
- Relocated Python, native libraries, model cache, Windows DLL dependencies and Mac permissions must survive packaging.

### Task 1: Package installer and readiness
Files: Assets/Webcam/Runtime/RuntimePackageInstaller.cs, StudyRuntimeBootstrap.cs, LocalStudyProcess.cs, LobbyStudyWebcam.cs; Tools/tests/RuntimePackageInstallerTests.cs.
- [x] Write/run failing installer tests for extraction, validation, cancellation, retry, cache and concurrency.
- [x] Implement ZIP64 installer with manifest hash, lock/staging/version/permission handling; rerun tests.
- [x] Connect background bootstrap and webcam readiness UI; verify Unity compile and current session flow.

### Task 2: Native payload and build pipeline
Files: Tools/distribution/prepare_runtime.py, tests, scripts; Assets/Webcam/Editor/DistributionBuild.cs; .gitignore, StreamingAssets.meta.
- [x] Write/run package layout/platform/config tests then implement native Python+dependency/model bundling with notices and relocation smoke test.
- [x] Add build gate and Mac/Windows entrypoints; ensure missing config/wrong target payload is rejected.
- [x] Prepare real Mac payload and prove relocated Python/QEV works offline.

### Task 3: Fresh game validation and delivery
Files: docs/distribution/DESKTOP.md, task handoff and validation records.
- [x] Build Mac standalone; test fresh runtime prep without developer runtime dependency and offline QEV bootstrap/inference on synthetic input.
- [x] Run Python/C# suites and Unity account/quiz/webcam regressions. Independent final code review, fix material findings with regression tests.
- [x] Record Windows native-build limitation if no Windows runner/module available; deliver scripts and real Mac artifact, preserve current integrated project.

Native Windows execution remains unverified as anticipated in Task 3; this is a delivered build path, not a verified Windows binary. Review identified runtime-use locking and native macOS minimum version mismatches; both fixed with failing regressions followed by passing suites. Actual macOS plist parser failure found during launch validation was fixed and gated with plutil.
