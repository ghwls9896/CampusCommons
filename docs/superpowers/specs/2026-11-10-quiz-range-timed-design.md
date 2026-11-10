# Page ranges and timed mixed-format quiz

User requirements: accept PDFs longer than 80 pages; choose inclusive start/end pages for generation; level1 multiple-choice10s, level2 short-answer15s, level3 written-answer20s; submit/timeout advances immediately with no intermediate answer/explanation; show concise answers/explanations together after all three. User explicitly chose Gemini semantic grading for typed answers.

Approach: retain the current white IMGUI. Remove80 as the document ceiling. Accept documents within10MiB; inspect total pages. Start/end fields default to the whole document up to1000 selected pages (provider PDF request ceiling); allow a narrow range from longer documents. Physically extract selected pages with pypdf before Gemini; map validated relative source_page to original PDF numbers. Source page1 is the physical first PDF page, not printed page labels. Both endpoints use the existing token/busy/size controls.

Question contract: levels1/2/3 carry answer_type multiple_choice/short_answer/written, model_answer, grading_criteria and short explanation/source. Only level1 has4 options and a correct_index; typed levels have empty options and correct_index=-1. Quiz result includes page_range start_page/end_page/total_pages. Client validates the generation matches the selected range.

Timing: monotonic real time,10/15/20 seconds; deadline equality counts as timeout. Submission records once then immediately advances; timeout automatically submits the existing selected choice or typed draft; an empty answer is incorrect. No choice selection is carried into another question. Returning from an exit confirmation continues the same deadline. Finishing triggers one semantic grading request for levels2/3. No automatic retry; grading failure leaves answers/explanations visible with an explicit retry and pending score. Blank/time-out answers are incorrect without a provider call. AI calls share the existing daily provider budget; grade route is bounded and authenticated. Solo demo results remain client-side, not trusted XP/reward evidence.

Scope: QuizRoom scripts/editor test, backend, Tools/tests, cc-2 docs; preserve Player/GameStart/scenes and unrelated EscapeGame edit. Native Unity version stays6000.4.5f1; disposable6000.6.0f1 for installed verification. Deploy coordinated server/client question contract before user testing. No actual personal PDF selected by the agent.

Validation: backend real PDF range extraction and source remapping, invalid range/auth/quota/grading outputs, 81+ page source; C# range/contract/deadline/timeout/automatic advance/end-only results/grading tests; Unity compile and scene lifecycle plus visual range and timer checks; live approved synthetic sample only.

References: https://ai.google.dev/gemini-api/docs/document-processing (PDF request up to1000 pages; context/rate limits still apply).
