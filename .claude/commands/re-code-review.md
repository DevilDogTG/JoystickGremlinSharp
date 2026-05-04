---
name: re-code-review
description: >
  Manually trigger a full code review after fixes have been applied following a prior review.
  Runs preflight checks (PR exists, prior review exists, new commits since last review) before
  performing and publishing a fresh full review to the PR.
---

# Re-Code-Review Workflow

Run a full structured code review after fixes have been applied. Enforces preflight checks to
ensure a PR and prior review exist and that at least one new fix commit has been pushed
since the last review, then publishes the new review to GitHub.

---

## Step 0 — Preflight Checks

Run all three checks before doing any review work. **Abort on first failure.**

### Check 1: PR Exists

```
gh pr list --head <current_branch> --state open --json number,url,headRefName
```

- ✅ Pass: an open PR exists for the current branch → extract `pr_number` and `pr_url`
- ❌ Fail → stop and report:
  ```
  ❌ Preflight failed: No open PR found for branch '<branch>'.
     Create a PR first (e.g., via `finish-feature`) then re-run this skill.
  ```

### Check 2: Prior Code Review Exists

```
gh pr view <pr_number> --json reviews
```

- Inspect the `reviews` array for at least one review with state `CHANGES_REQUESTED`, `APPROVED`, or `COMMENTED`
- ✅ Pass: at least one prior review found → extract `review_id`, `submitted_at`, review state, and body
- ❌ Fail → stop and report:
  ```
  ❌ Preflight failed: No prior code review found on PR #<N>.
     Run the `code-review` skill for the initial review instead.
  ```

### Check 3: Fix Commits Exist Since Last Review

```
git log --oneline origin/main.. --after="<last_review_submitted_at>"
```

- ✅ Pass: at least one new commit exists since the last review was submitted
- ❌ Fail → stop and report:
  ```
  ❌ Preflight failed: No new commits since last review (<review_date>).
     Apply and push your fixes, then re-run re-code-review.
  ```

---

## Step 1 — Full Structured Code Review

All three preflights passed. Perform a complete review following the **C# & Avalonia Code Review** checklist:

### Review scope

- Diff: `git diff origin/main..HEAD` (all changes on this branch)
- Focus extra attention on files changed since the last review (`git diff <last_review_commit>..HEAD`)
- Note which prior findings are now resolved vs. which persist or have regressed

### Review categories (same as code-review skill)

Work through each category and report findings as **[CRITICAL]**, **[WARNING]**, or **[STYLE]**:

1. **Architecture & Separation of Concerns**
2. **Threading & Safety**
3. **Memory & Object Lifetime**
4. **ReactiveUI Patterns & Bindings**
5. **XAML & Binding Correctness**
6. **C# & .NET Correctness**

### Summary table

```
| Category                      | CRITICAL | WARNING | STYLE |
|-------------------------------|----------|---------|-------|
| Architecture & Separation     |    0     |    0    |   0   |
| Threading & Safety            |    0     |    0    |   0   |
| Memory & Lifetime             |    0     |    0    |   0   |
| ReactiveUI & Bindings         |    0     |    0    |   0   |
| XAML & Binding Correctness    |    0     |    0    |   0   |
| C# & .NET Correctness         |    0     |    0    |   0   |
```

**Overall verdict** — one honest sentence.

### Prior-findings delta

After the summary table, include a delta section comparing this review against the last:

```
## Changes Since Last Review

| Prior Finding                            | Severity | Status          |
|------------------------------------------|----------|-----------------|
| Null-dereference in FooService.Bar()     | CRITICAL | ✅ Resolved     |
| async void in ViewModel.LoadAsync        | WARNING  | ❌ Persists     |
| Missing StringComparison.Ordinal         | STYLE    | ✅ Resolved     |
```

### Top 3 fixes (if any remain)

Show corrected code side-by-side with the original for the highest-impact remaining issues.

---

## Step 2 — Publish New Review to GitHub

Choose the review state based on remaining findings:

| Condition                          | `gh pr review` state  |
|------------------------------------|-----------------------|
| Zero CRITICAL + zero WARNING       | `--approve`           |
| Warnings only (no CRITICAL)        | `--comment`           |
| Any CRITICAL findings remain       | `--request-changes`   |

> **Note**: GitHub does not allow the PR author to submit `APPROVE` or `REQUEST_CHANGES` on
> their own PR. In that case, use `--comment` and add a note at the top of the review body:
> *"[Author review — cannot approve own PR; verdict: Approved / Changes Requested]"*

Publish:
```
gh pr review <pr_number> --<state> --body "<full_review_markdown>"
```

Verify the review appears on the PR.

---

## Step 3 — Post Reply to Prior Review Comment

In addition to the new top-level PR review, post a **reply to the original review comment thread**
summarising what changed:

```
gh api repos/{owner}/{repo}/pulls/comments/{prior_comment_id}/replies \
  -f body="<re-review summary>"
```

Reply body template:
```markdown
## Re-review after fixes

- Prior review: <date> (<state>)
- Commits since last review: <N>

**Resolution delta**: <N> resolved, <N> outstanding
**New verdict**: <Approved | Comment | Request Changes>

See the [new full review](<review_url>) for complete details.
```

---

## Step 4 — Terminal Summary

```
✅ Re-Code-Review complete — PR #<N>
──────────────────────────────────────────
Prior review:   <date>  (<prior_state>)
New commits:    <N> since last review
──────────────────────────────────────────
Findings:   <N> CRITICAL  <N> WARNING  <N> STYLE
Delta:      ✅ <resolved>  ❌ <persists>  🆕 <new>
──────────────────────────────────────────
Verdict:    <Approved | Comment | Request Changes>
Review URL: <github_review_url>
```

**Workflow ends here.** The user decides the next action.

---

## Implementation Notes

- Always run all three preflight checks before any review work; abort on first failure
- The "new review" is a first-class PR review (not a comment), so it shows in the PR timeline
- The reply to the prior review comment provides continuity of the review thread
- Never auto-fix code, merge, or push — this skill is read + publish only
- Use `git diff origin/main..HEAD` for the full branch diff
- Use `git log --oneline origin/main..HEAD --after="<date>"` for commit count since last review
