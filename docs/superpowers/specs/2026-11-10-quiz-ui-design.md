# Quiz UI refinement

User request: fix broken Korean, remove the two bottom messages, show selected PDF size and pages with explicit limit warnings, show real loading state, and replace the dark UI with a polished white UI.

Approach: retain the existing Unity IMGUI integration and server-only Gemini key. Normalize visible text to NFC without altering filesystem paths. Reuse pypdf on the server for authenticated PDF inspection; counting PDF objects with regex would fail for compressed object streams, while a new Unity PDF library would add dependencies. Add `POST /v1/pdfs/inspect` returning size_bytes, page_count, max_size_bytes, max_pages and within_limits; it does not call Gemini or consume daily generation budget. An oversized local file is blocked before upload. Keep server generation validation authoritative.

Visual design: white rounded panel, warm gray background, navy primary action, quiet slate typography, document information card, red inline validation, numbered learning steps. Generation displays animated activity, upload/AI stages and elapsed time without inventing completion percentages. Apply the same theme to questions, feedback, results and desktop file browser. Remove the permanent provider-transmission footer and replace the old page-limit error with an inline selected-file warning.

Constraints: 10 * 1024 * 1024 bytes, 80 pages, desktop Windows/macOS, Unity 6000.4.5f1 repository unchanged. Use the installed 6000.6.0f1 only in the disposable preview. No Player/GameStart/scene changes. Preserve the current question JSON contract. No automatic paid fallback. Cancel/exit must dispose requests and ignore unfinished inspection.

Verification: failing backend inspection tests then full pytest; pure C# NFC/metadata/boundary tests; compile/play-mode in the disposable project and screenshot checks of empty, selected, limit and generating states. Production backend must expose the inspection route before the new UI is used. Live generation uses only the already authorized synthetic sample.
