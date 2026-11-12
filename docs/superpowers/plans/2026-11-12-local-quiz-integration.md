# Local Quiz Integration Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add the existing PDF quiz room to the user's current local account/webcam/team integration.
**Architecture:** Merge origin/codex/pdf-quiz-room into codex/local-integration, resolve shared Player/package conflicts by preserving both behaviors, and exercise the real scene with fixture authentication and quiz responses. Preserve the ignored local deployment configuration without exposing its token.
**Tech Stack:** Unity 6000.4.5f1, C#, Python/FastAPI.
**Spec:** User request (current local integration); docs/QUIZ_ROOM_SETUP.md and docs/tasks/cc-2.json from the quiz branch.

## Global Constraints
- Keep existing local Sunbox assets and their GUIDs; never commit credentials.
- Retain avatar, escape, account and webcam behavior; do not rewrite the shared scene.
- Local integration only; no GitHub main merge or deployment change.
- Use the existing linked worktree; no new user-facing project copies.

## Review Focus
- Quiz input must not activate avatar C/R/V controls or move the player.
- Authentication must block quiz input and pending requests before login/after logout.
- Leaving the quiz must clear answers and requests, with exit cancellation preserving answers.
- Returning to the lobby must permit seating and webcam shutdown/standing.
- Local quiz deployment settings must remain ignored and usable.

### Task 1: Merge and verify the local integrated experience
**Files:** Assets/GrandLibrary/Scripts/Player.cs; GameStart.cs; Packages/manifest.json; Packages/packages-lock.json; Assets/IntegrationAudit/Editor/LocalIntegrationAudit.cs; existing Assets/QuizRoom/ and backend/.
**Interfaces:** GameStart attaches QuizRoom; Player observes QuizRoom.BlocksMovement; AccountWorldGate disables/enables quiz; LocalIntegrationAudit authenticates via fixture before QuizRoomTest.Run.
- [x] Inspect merge-tree conflicts and preserve pre-merge commit 2da9840 for rollback.
- [x] Merge existing tested quiz implementation without committing; resolve Player using both original intents, package set as union.
- [x] Extend existing integration audit for authenticated quiz lifecycle and account blocking/seat transition; reproduce any integration defect before fixing it.
- [x] Run backend pytest, pure C# quiz tests, team tests, and Unity 6000.4.5f1 authenticated quiz/account/webcam/escape checks. Expected: all test success markers, no game compilation/runtime errors.
- [x] Review diff; commit only merge/code/docs/meta, never local secrets or Sunbox package. Update folder guide, reopen integrated project for user.
