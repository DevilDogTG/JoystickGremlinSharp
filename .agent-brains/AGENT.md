---
version: 1.0
profiles:
  - base-developer
  - charp-developer
strict_override: false
---

# Workspace Instructions

## Overview
Project-specific context and local overrides for JoystickGremlinSharp.

## Workspace Rules
<!-- begin:framework -->
<!-- Global and profile rules are active automatically. Add project-specific overrides here. -->
<!-- end:framework -->

## Git Workflow

See `.agent-brains/memory/git-workflow.md` for the full rebase guide.

**Key rules:**
- Always use `git rebase origin/main` (never `git merge main` or `git pull` on a feature branch)
- Always use `git push --force-with-lease` after a rebase
- Check `git log --oneline --graph` before pushing — look for merge commits or duplicate messages
- If a botched push creates a spurious PR, close it immediately with `gh pr close`
