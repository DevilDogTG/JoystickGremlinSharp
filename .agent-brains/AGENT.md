---
version: 1.0
profiles:
  - base-developer
  - csharp-developer
  - github-scm
strict_override: false
---

# Workspace Instructions

## Overview
Project-specific context and local overrides for JoystickGremlinSharp.

## Workspace Rules
<!-- begin:framework -->
<!-- Global and profile rules are active automatically. Add project-specific overrides here. -->
<!-- end:framework -->

### Shared-Collection Concurrency (settings ↔ background threads)
Any collection on `AppSettings` (or similar singleton state) that a background thread
reads — e.g. `AutoLoadTriggers`, enumerated by `ProcessMonitorService` off the UI
thread — is **replace-only**: writers build a fresh list and swap the property
reference atomically; neither the list nor any element instance already published may
be mutated in place. UI row ViewModels must snapshot into NEW model instances on save
(`ToTrigger()` pattern), never write through to instances they were initialized from.
Reference: PR #71 review round 1 (torn-read defect) + `AppSettings.AutoLoadTriggers`
doc remarks.

### Primary-Constructor Heuristic
When a class's traditional constructor body only assigns its parameters to private readonly fields of the same name (modulo `_` prefix), convert to a C# 12 primary constructor and capture the parameters directly in member bodies. A computed field with non-trivial RHS (e.g. `_ownExePath = Environment.ProcessPath ?? ...`) stays as a field initializer; the captured parameters drop the backing field. This satisfies csharp-developer §47 ("prefer primary constructors where they improve clarity") with a concrete trigger. Reference: `HidHideManager.cs` after PR #69 commit `3032cb2b`.

### Self-Review Publishing Convention
Agent-authored code reviews on the agent's own PRs are published with
`gh pr review --comment` — GitHub blocks APPROVE/REQUEST_CHANGES on one's own PR.
The review body MUST lead with an explicit verdict line
(`**Verdict: Approved**` / `Changes Requested`); merge decisions read that verdict
line, not the GitHub review state. Body-only reviews have no inline threads, so
fix-resolution summaries go in a regular PR comment referencing the review id.
Established PR #74 (2026-06-05); reaffirmed both rounds of PR #77.

## Git Workflow

See `.agent-brains/memory/git-workflow.md` for the full rebase guide.

**Key rules:**
- **No direct pushes to `main` — ever.** ALL commits, including brains/plan/memory
  housekeeping and session handover memos, go through a branch + PR (use `chore/*`
  for housekeeping) and rebase-merge. Established 2026-06-05 (PR #75 replaced the
  old direct-commit convention for brains records).
- Always use `git rebase origin/main` (never `git merge main` or `git pull` on a feature branch)
- Always use `git push --force-with-lease` after a rebase
- Check `git log --oneline --graph` before pushing — look for merge commits or duplicate messages
- If a botched push creates a spurious PR, close it immediately with `gh pr close`
