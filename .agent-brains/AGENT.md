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

### Primary-Constructor Heuristic
When a class's traditional constructor body only assigns its parameters to private readonly fields of the same name (modulo `_` prefix), convert to a C# 12 primary constructor and capture the parameters directly in member bodies. A computed field with non-trivial RHS (e.g. `_ownExePath = Environment.ProcessPath ?? ...`) stays as a field initializer; the captured parameters drop the backing field. This satisfies csharp-developer §47 ("prefer primary constructors where they improve clarity") with a concrete trigger. Reference: `HidHideManager.cs` after PR #69 commit `3032cb2b`.

## Git Workflow

See `.agent-brains/memory/git-workflow.md` for the full rebase guide.

**Key rules:**
- Always use `git rebase origin/main` (never `git merge main` or `git pull` on a feature branch)
- Always use `git push --force-with-lease` after a rebase
- Check `git log --oneline --graph` before pushing — look for merge commits or duplicate messages
- If a botched push creates a spurious PR, close it immediately with `gh pr close`
