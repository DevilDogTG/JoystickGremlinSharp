# Git Workflow Guide

## Branch Strategy Recap

```
main          ← primary branch; all feature/bugfix PRs target here
feature/xyz   ← branch from main; rebase on main before opening PR; rebase-merge into main
bugfix/xyz    ← same as feature; branch from main; rebase on main before opening PR
```

**Enforcement**: rebase-merge only — no merge commits, no squash merges (Settings → Pull Requests).

---

## Correct Rebase Workflow (Before Opening a PR)

### Step 1 — Sync local main

```powershell
git fetch origin
git checkout main
git reset --hard origin/main
```

### Step 2 — Rebase your branch

```powershell
git checkout feature/my-branch
git rebase origin/main
```

If there are conflicts, for each conflicted file:
```powershell
# Edit the file to resolve the conflict markers (<<<<, ====, >>>>)
git add <file>
git rebase --continue   # repeat until rebase completes
# To abort and start over:
git rebase --abort
```

### Step 3 — Force-push (required after rebase)

```powershell
git push --force-with-lease origin feature/my-branch
```

> ⚠️ Use `--force-with-lease` (not `--force`) — it fails safely if someone else pushed to the remote branch since your last fetch.

---

## ❌ What NOT to Do

| Action | Why it's wrong |
|---|---|
| `git pull` on a feature branch | Creates a merge commit — violates linear history policy |
| `git merge main` into feature branch | Same — creates merge commit |
| `git push --force` without `--lease` | Unsafe — can silently overwrite remote work |
| Pushing after a broken rebase without checking `git log --graph` | Creates duplicate commits + merge commits |

---

## Recovering from a Botched Rebase

This is what to do when the branch ends up with duplicate commits or a merge commit (like the v10.5.1 incident).

### Scenario A — Branch fixes are already on main (PR merged)

Simply reset the branch to main:

```powershell
git fetch origin
git checkout feature/my-branch
git reset --hard origin/main
git push --force-with-lease origin feature/my-branch
```

Close any spurious PRs that were opened by the bad push:
```powershell
gh pr close <PR_NUMBER> --comment "Closing — created accidentally during botched rebase."
```

### Scenario B — Branch fixes are NOT yet on main (PR still open)

Find the last good commit on your branch (the real work, before the mess):

```powershell
git log --oneline --graph   # identify the last real commit SHA
git reset --hard <LAST_GOOD_SHA>
git push --force-with-lease origin feature/my-branch
```

### Scenario C — Unsure where your work is

Check what's different between your branch and main:
```powershell
git --no-pager log --oneline origin/main..HEAD   # commits only on your branch
git --no-pager diff origin/main...HEAD           # code diff
```

---

## Diagnosing a Messy Graph

```powershell
git --no-pager log --oneline --graph -20
```

**Red flags to look for:**

```
*   abc1234  Merge branch 'feature/x' of github.com:... into feature/x   ← merge commit: BAD
|\  
| * def5678  ...your original commits...
* | ghi9012  ...duplicate rebased commits...
|/  
```

This pattern means:
1. Your branch had commits A, B
2. They were rebased → became A', B' on main
3. Old commits A, B were then pushed again → created the merge above

**Fix**: identify which version is on `origin/main`, reset your branch to there.

---

## Checklist Before Every Push

```
[ ] git log --oneline --graph  → no merge commits, no duplicate messages
[ ] git status                 → clean working tree
[ ] git push --force-with-lease (if rebased; plain push if branch is new)
[ ] gh pr list --head <branch> → no stray open PRs from previous botched pushes
```

---

## Quick Reference

```powershell
# Sync main
git fetch origin && git checkout main && git reset --hard origin/main

# Rebase feature branch on main
git checkout feature/my-branch
git rebase origin/main
git push --force-with-lease origin feature/my-branch

# Verify clean graph
git --no-pager log --oneline --graph -15

# Emergency reset to main (branch already merged)
git reset --hard origin/main && git push --force-with-lease origin feature/my-branch
```
