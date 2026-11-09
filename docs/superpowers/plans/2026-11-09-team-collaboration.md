# Team collaboration implementation plan

Goal: give all four members and their AI agents the same task boundaries, isolated branches, handoff format and enforceable PR checks.
Architecture: main is reviewed integration; one GitHub Issue, short-lived branch, worktree and task JSON per change. AGENTS.md is the shared entry point; GitHub Issues/PRs hold current coordination. Branch-local files do not act as distributed locks.
Decisions: user explicitly asked to implement proper shared collaboration; use defaults within that authorization without another design approval loop. User provided all four owners. Bootstrap on origin/main, independently of quiz commit 30873a0. Managed worktree API rejected nested repo sourceRoot; use sibling git worktree fallback. Preserve pre-existing EscapeGame CRLF-only diff.

- [x] Write common instructions, domain ownership, migration/runbook and tool-specific entry points.
- [x] Implement and test commit-scope/Unity hygiene/task checks with failing fixtures first; wire secret-free PR CI.
- [x] Prepare CODEOWNERS, issue/PR templates and main protection payload; distinguish prepared files from applied admin settings.
- [x] Record actual tasks and publish coordination and quiz on separate branches/PRs; inspect remote checks.
- [x] Independent review, fix findings, document remaining administrator action. Never merge own work or claim branch protection enabled without verification.

Review focus: cannot overwrite teammates' work, claim a file lock from unmerged JSON, falsely report Unity validation, put credentials in the client/repo, or treat a changed task scope as permission from the affected human.

Verification: 16 stdlib tests passed; exact committed scopes passed for collaboration (21 paths before this handoff) and quiz (31 paths). GitHub Team checks run 42572707302 succeeded on fb67dee. Independent read-only review findings for folder meta and Unity hidden files were reproduced and fixed with regression tests, including visible parent folders and StreamingAssets exceptions. Upstream main advanced to eb786c2 during work; integrated it and standardized Tools/ casing. No product files changed relative to main in collaboration PR.

Published PRs: #3 collaboration, #4 quiz (Draft). Remaining administrator actions are main protection and Yeni-sjs Write access; not claimed complete or auto-applied. Main protected=false verified through API. Native worktree tool rejected nested repository; sibling worktree used.
