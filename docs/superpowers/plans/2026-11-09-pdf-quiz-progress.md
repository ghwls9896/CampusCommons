# Progress — 2026-11-09-pdf-quiz.md

- Base: 3344d1c; fresh clone on codex/pdf-quiz-room. Initial EscapeGame.cs diff is pre-existing CRLF normalization from repository attributes; do not include in feature.
- Ruling: use fresh feature-branch clone for isolation; no second full asset checkout/worktree required.
- Ruling: user's explicit implementation request accepts the previously discussed design. Proceed inline without another approval loop.
- Shared interface checked: backend snake_case fields exactly match JsonUtility client fields.
- Live API key is not available; use a fake upstream only in tests and explicitly report live generation as unverified.

## Completion evidence

- Backend: Python 3.14.2, `cd backend && .venv/bin/python -m pytest -q`: 26 passed. Initial tests failed before implementation; additional upload/generation timeout tests observed RED then GREEN. One dependency deprecation warning from Starlette's httpx TestClient adapter.
- Pure C#: Unity-bundled mcs compiled Tools/tests/QuizSessionTests.cs plus QuizData.cs/QuizConnection.cs; mono output `QUIZ_SESSION_PASS 20 checks`.
- Unity import: installed 6000.6.0f1 in disposable /private/tmp/CampusCommons-quiz-validation, exit 0, no C# compile errors. Original project remains 6000.4.5f1.
- Unity playmode: `-executeMethod CampusCommons.Quiz.Editor.QuizRoomTest.Run`, exit 0, `PDF_QUIZ_ROOM_PASS`. Verifies scene attachment, real portal entry, cursor, fixture quiz flow, exit cancel, reset and pending-request disposal. Runtime errors fail the test; known editor-only SearchDatabase indexing exception is explicitly excluded. Test request is unsent, so this is cleanup coverage, not an actual delayed-network cancellation test.
- Real local uvicorn HTTP smoke: `/healthz` returned 200 and an unconfigured quiz endpoint returned the expected structured 503.
- One fresh read-only reviewer found no critical/important issue; noted the network-cancellation coverage limit above. Rulings: live Gemini/account eligibility, deployed Render endpoint, standalone Windows/macOS builds remain external/manual verification. No claim of a live generation test.
- Browser is part of QuizRoom rather than a separate PdfBrowser file to keep this small IMGUI feature contained.
- GitHub CLI reports viewerPermission READ. Keep feature locally and provide a patch; no remote push, merge, fork, account creation or deployment performed.

## User setup remaining

Push/apply feature using a write-authorized GitHub account; obtain free Gemini key; create Render Free service from render.yaml; set server-only key and share the generated quiz access token; enter server HTTPS URL and token in Unity. Follow docs/QUIZ_ROOM_SETUP.md and verify a real study PDF end to end.

## Live free deployment and user testing — 2026-11-10

- Later verified state supersedes the initial Read-only/offline-account notes above. PR #4 is shared under HIGHCHOI Write access; main collaboration changes are merged into the feature branch.
- Independent CampusCommons Google project and dedicated API key created with explicit user approval. Free tier confirmed; Gemini key stored only on Render.
- Render Free https://campuscommons-quiz.onrender.com is live. Billing shows Hobby, no card on file, current service/pipeline charges$0.00. Auto deploy disabled.
- Player setup simplified to PDF picker and generate. Operator-only ignored Resources/QuizConnection.json carries the endpoint and demo access code; it never contains the Gemini key.
- Gemini3.8 basic/LOW requests timed out with ReadTimeout. Manual selection of free gemini-3.5-flash-lite produced actual PDF-based questions; repository deployment defaults now match that working model.
- Unity6000.6.0f1 disposable preview: actual native PDF picker → Render/Gemini generated3levels → two correct/one intentionally wrong submission → answer/explanation/page quotes → final2/3 → reset to empty picker all manually verified. This is separate from the fixture lifecycle test, which also passed on final UI code.
- Backend26, C#27, team16, scope34 checks and PR CI passed. Original6000.4.5f1/standalone builds remain unverified. User's real study PDF and human review/merge remain their next steps.
