# Progress — 2026-11-09-pdf-quiz.md

- Base: 3344d1c; fresh clone on codex/pdf-quiz-room. Initial EscapeGame.cs diff is pre-existing CRLF normalization from repository attributes; do not include in feature.
- Ruling: use fresh feature-branch clone for isolation; no second full asset checkout/worktree required.
- Ruling: user's explicit implementation request accepts the previously discussed design. Proceed inline without another approval loop.
- Shared interface checked: backend snake_case fields exactly match JsonUtility client fields.
- Live API key is not available; use a fake upstream only in tests and explicitly report live generation as unverified.

## Completion evidence

- Backend: Python 3.14.2, `cd backend && .venv/bin/python -m pytest -q`: 26 passed. Initial tests failed before implementation; additional upload/generation timeout tests observed RED then GREEN. One dependency deprecation warning from Starlette's httpx TestClient adapter.
- Pure C#: Unity-bundled mcs compiled tools/tests/QuizSessionTests.cs plus QuizData.cs/QuizConnection.cs; mono output `QUIZ_SESSION_PASS 20 checks`.
- Unity import: installed 6000.6.0f1 in disposable /private/tmp/CampusCommons-quiz-validation, exit 0, no C# compile errors. Original project remains 6000.4.5f1.
- Unity playmode: `-executeMethod CampusCommons.Quiz.Editor.QuizRoomTest.Run`, exit 0, `PDF_QUIZ_ROOM_PASS`. Verifies scene attachment, real portal entry, cursor, fixture quiz flow, exit cancel, reset and pending-request disposal. Runtime errors fail the test; known editor-only SearchDatabase indexing exception is explicitly excluded. Test request is unsent, so this is cleanup coverage, not an actual delayed-network cancellation test.
- Real local uvicorn HTTP smoke: `/healthz` returned 200 and an unconfigured quiz endpoint returned the expected structured 503.
- One fresh read-only reviewer found no critical/important issue; noted the network-cancellation coverage limit above. Rulings: live Gemini/account eligibility, deployed Render endpoint, standalone Windows/macOS builds remain external/manual verification. No claim of a live generation test.
- Browser is part of QuizRoom rather than a separate PdfBrowser file to keep this small IMGUI feature contained.
- GitHub CLI reports viewerPermission READ. Keep feature locally and provide a patch; no remote push, merge, fork, account creation or deployment performed.

## User setup remaining

Push/apply feature using a write-authorized GitHub account; obtain free Gemini key; create Render Free service from render.yaml; set server-only key and share the generated quiz access token; enter server HTTPS URL and token in Unity. Follow docs/QUIZ_ROOM_SETUP.md and verify a real study PDF end to end.
