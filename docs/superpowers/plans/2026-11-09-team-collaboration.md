# Team collaboration implementation plan

Goal: give all four members and their AI agents the same task boundaries, isolated branches, handoff format and enforceable PR checks.
Architecture: main is reviewed integration; one GitHub Issue, short-lived branch, worktree and task JSON per change. AGENTS.md is the shared entry point; GitHub Issues/PRs hold current coordination. Branch-local files do not act as distributed locks.
Decisions: user explicitly asked to implement proper shared collaboration; use defaults within that authorization without another design approval loop. User provided all four owners. Bootstrap on origin/main, independently of quiz commit 30873a0. Managed worktree API rejected nested repo sourceRoot; use sibling git worktree fallback. Preserve pre-existing EscapeGame CRLF-only diff.

- [ ] Write common instructions, domain ownership, migration/runbook and tool-specific entry points.
- [ ] Implement and test commit-scope/Unity hygiene/task checks with failing fixtures first; wire secret-free PR CI.
- [ ] Prepare CODEOWNERS, issue/PR templates and main protection payload; distinguish prepared files from applied admin settings.
- [ ] Record actual tasks and publish coordination and quiz on separate branches/PRs; inspect remote checks.
- [ ] Independent review, fix findings, document remaining administrator action. Never merge own work or claim branch protection enabled without verification.

Review focus: cannot overwrite teammates' work, claim a file lock from unmerged JSON, falsely report Unity validation, put credentials in the client/repo, or treat a changed task scope as permission from the affected human.
