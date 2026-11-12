# Study records implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Background local classification, stable labels, actual study time, account-owned durable records.
**Architecture:** Pure C# evidence/timeline and outbox; Unity owns lifecycle/UI and authenticated API; Supabase Edge authenticates and SQL serializes immutable segment appends. Existing account and QEV remain intact.
**Tech Stack:** Unity6000.4.5f1/C#, Python QEV fast, Deno Edge, PostgreSQL.
**Spec:** docs/superpowers/specs/2026-11-12-study-records.md

## Global Constraints
- 3-second recent weighted scores, study3s/nonstudy2s/drowsy8s, gap>3s uncounted.
- Five labels only. Startup/error/paused are operational messages, not behavior classes.
- Background focus must not stop active inference; actual pause/sleep closes evidence interval.
- No private webcam access for agent validation. No images stored/transmitted externally.
- 30-second sync, Korean calendar day, immutable sequence/idempotency and account isolation.
- Keep unrelated untracked assets and previous commits. No main merge.

## Review Focus
- Slow inference, sleep, wall-clock jumps cannot inflate duration.
- Alternating nonstudy categories must stop credit; drowsy display delay cannot add study time.
- Logout/account switch during upload must preserve original ownership and ignore late responses.
- Retry/crash/restart and midnight must not duplicate/lose acknowledged totals.
- Concurrent/reordered/foreign/malformed segment writes fail without touching existing accounts.

### Task 1: evidence and local records
Files: Assets/Webcam/Runtime/StudyTracker.cs, StudyRecords.cs, Tools/study/StudyTests.cs.
Interfaces: StudyTracker.Observe(captureSeconds, scores), Break(), Finish(); committed StudyInterval(start,end,state); StudyStore queues account/session/sequence records, pending-ms by KST day.
- [ ] RED: steady study backcredits initial3s, one-frame error recovers, sustained/mixed nonstudy stops, drowsy8s display, longgap/reset, midnight; duplicate ack/outbox reload/account split.
- [ ] GREEN implement bounded evidence, pending transition and committed intervals; atomic outbox serializer.
- [ ] Run standalone Mono tests, commit.

### Task 2: owned server API
Files: supabase/functions/_shared/study.ts, study/index.ts, tests/study_test.ts; new migration; Tools/study/database_tests.py.
Interfaces: POST study {action:sync, session_id, started_ms, segments:[{sequence,start_ms,end_ms,state}], ended_ms}; response {user_id,session_id,ack_sequence,today_ms,date}; POST {action:summary} same today's aggregate with session list.
- [ ] RED Edge tests auth/shape/limits; SQL tests owner/RLS/idempotency/overlap/reorder/midnight.
- [ ] GREEN tables/RPC and handler with authenticated identity; no client level or total writes.
- [ ] Run local disposable PostgreSQL and Deno tests; commit.

### Task 3: Unity integration
Files: AccountSession.cs narrow StudyCall<T>; LobbyStudyWebcam.cs; StudyRecordService.cs; Editor no-camera verification.
- [ ] RED test focus(false) retains active measurement; API/account late-response cancellation; panel times and persistence.
- [ ] GREEN background runtime flag, capture timestamps and score DTO into tracker, stop/pause/account cleanup, thirty-second sync and retry, today's total/pending/history UI.
- [ ] Run Unity6000.4.5f1 no-camera integration and existing account/quiz/webcam regression; commit.

### Task 4: deploy and deliver
- [ ] Fresh independent whole-change review, one focused fix pass with regression tests.
- [ ] Apply additive migration and deploy study function to existing project; verify authenticated ownership with temporary rollback-only database data and public unauth rejection.
- [ ] Mac rebuild, confirm metadata/startup, preserve old running app until replacement ready. Live user webcam is manual only.
- [ ] Update same issue/PR and handoff, run scoped checks, push; report exact tests and remaining limits.
