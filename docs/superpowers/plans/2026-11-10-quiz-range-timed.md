# Page ranges and timed quiz implementation plan

**Goal:** allow long PDFs with range selection and a timed10/15/20-second mixed-format quiz with final-only review and AI grading.
**Architecture:** Unity inspection/range -> pypdf slice -> Gemini question JSON -> timed local session -> bounded semantic grading -> final review.
**Spec:** docs/superpowers/specs/2026-11-10-quiz-range-timed-design.md

## Review focus
- Inclusive page ranges, final original page numbers, and only selected content forwarded.
- Documents81+ pages and selected pages81+ allowed; one request up to1000 pages.
- Deadline equality, inactivity, empty response and rapid double submit never regrade/skip a level.
- No answers or feedback between questions; typed answers and timeout state survive final review.
- Cancel/reselection/exit and grading failures preserve valid state and bounded quota.

## Tasks
- [x] Write failing backend tests for PDF range validation/extraction/mapping; implement range metadata and selection.
- [x] Write failing mixed-format/schema/grading tests; implement Gemini generation and semantic grade API.
- [x] Write failing C# document-range and10/15/20-second session tests; implement contract/state.
- [x] Connect white UI range fields, typed inputs, timer, immediate transitions and concise final review.
- [x] Validate full test suites, disposable Unity and fresh code review; fix important findings.
- [ ] Update task/API/handoff, push same PR, deploy and verify live synthetic flow; leave Unity ready.

Evidence: backend50, session35, document21, team16 checks pass; disposable Unity6000.6 lifecycle PASS. Review found deadline submission race and cross-page PDF cloning through links/article beads; both reproduced in failing tests and fixed. AI-only range copies remove navigation annotations/article-thread references; original PDF unchanged.

Note fidelity: selected-page FreeText/highlight/ink annotations are copied after removing navigation/page backreferences; linked excluded pages are not retained. The FreeText text and appearance stream regression passes.

## Follow-up implementation
- [x] Weighted10-point typed-answer rubrics, bounded partial marks and derived totals; backend52 tests.
- [x] Record each elapsed solve time, clamp timeout duration and show total; C#37 session checks.
- [x] Circular seconds countdown plus existing progress bar, final points/duration/rubric breakdown.
- [ ] Recheck Unity, deploy matched scoring contract and validate live partial grading.
