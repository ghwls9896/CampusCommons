# PDF quiz room

User-approved direction: Render Free backend and Gemini Free API; one person uploads a PDF and solves one question at each of levels 1, 2 and 3, then sees answers and explanations. Windows/macOS Unity clients share the backend.

The existing Quiz room entry opens a Korean quiz overlay. Four choices per question; level 1 recalls concepts, level 2 applies them, level 3 compares or reasons. One generation request creates all three questions, explanations and physical PDF page references. Answers are revealed only after submission in the UI; this is a local practice mode, not a competitive anti-cheat system.

Approaches: external Gemini API + Render was selected by the user over operating a model on Modal/ZeroGPU. Keep the provider in one backend module so it can be replaced. Do not introduce paid fallback, a database, accounts, rewards or multiplayer in this change.

Backend: Python FastAPI, bounded raw PDF upload, structural PDF validation, Gemini REST JSON-schema output, validated response. Maximum 10 MiB and 80 pages for this MVP. No durable PDF storage or prompt logging. A shared team access code and a process-local daily request ceiling protect the demo endpoint; these are not per-user authentication or provider billing controls. One worker, one generation at a time. No automatic generation retries.

Client: existing IMGUI conventions, bundled Korean font, platform-independent in-game PDF browser, editable server URL and team code. Opening quiz UI pauses movement, releases cursor, and preserves existing Escape exit confirmation. Leaving cancels upload and clears practice state; cancelling exit preserves it. Warm server on entry, allow explicit retry after errors. Deployment configuration and Korean setup guide included.

Validation: backend unit/API/provider-contract tests, Unity state and integration checks, compilation and a local HTTP smoke test. Live Gemini generation and public deployment require the user's account/key and are reported separately if unavailable. Repository targets Unity 6000.4.5f1; local 6000.6.0f1 verification must not silently upgrade the repository.
