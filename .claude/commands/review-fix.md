---
name: review-fix
description: >
  Verify that the latest commits resolve the findings from the last code review on the PR.
  Run this after applying fixes to check whether all blocking issues are resolved before
  requesting a full re-review or merging.
---

# Review-Fix Workflow

Check whether the fixes applied since the last PR code review have resolved all reported findings.
Posts a reply to the original review comment with a fix-resolution summary.

> **Purpose**: Lightweight verification — did the fix address what the review asked for?
> Use **re-code-review** when you want a full fresh code review pass.

---

## Step 1 — Identify PR and Last Review

1. Get the current branch name (`git branch --show-current`)
2. Find the open PR for this branch:
   ```
   gh pr list --head <branch> --state open --json number,url,headRefName
   ```
3. Retrieve the last code review on the PR:
   ```
   gh pr view <pr_number> --json reviews
   ```
   - Locate the **most recent review** that has a state of `CHANGES_REQUESTED` or `COMMENTED`
   - Extract the `review_id`, reviewer, submitted time, and the review body (findings list)
4. If no prior code review exists → stop and report:
   ```
   ⚠️  No prior code review found on PR #<N>. Run `code-review` for the initial review.
   ```

---

## Step 2 — Check That Fixes Are Present

1. Get the commits on this branch since the last review was submitted:
   ```
   git log --oneline origin/main.. --after="<review_submitted_at>"
   ```
2. If **no new commits** exist since the last review → stop and report:
   ```
   ⚠️  No new commits found since the last review (<review_date>).
       Apply your fixes and push before running review-fix.
   ```

---

## Step 3 — Analyse Fix Coverage

For each finding in the last review (`[CRITICAL]`, `[WARNING]`, `[STYLE]`):

1. Extract the file path and issue description from the review body
2. Check the current diff for that file (`git diff origin/main -- <file>`) and the new commits
3. Determine resolution status for each finding:
   - **✅ Resolved** — the code change directly addresses the reported issue
   - **⚠️ Partially resolved** — the change improves the issue but doesn't fully fix it
   - **❌ Not resolved** — no relevant change found, or the issue persists

Build a resolution table:

```
| Finding                                  | Severity | Status          |
|------------------------------------------|----------|-----------------|
| Null-dereference in FooService.Bar()     | CRITICAL | ✅ Resolved     |
| async void in ViewModel.LoadAsync        | WARNING  | ❌ Not resolved |
| Missing StringComparison.Ordinal         | STYLE    | ⚠️ Partial      |
```

---

## Step 4 — Compute Overall Verdict

| Condition                               | Verdict            |
|-----------------------------------------|--------------------|
| All CRITICAL + WARNING resolved         | ✅ **Ready to merge** (re-review can approve) |
| All CRITICAL resolved, warnings remain  | ⚠️ **Blocking issues resolved** (warnings outstanding) |
| Any CRITICAL not resolved               | ❌ **Still blocked** (must fix before merge) |

---

## Step 5 — Post Fix-Summary Reply

Post a **reply to the original review comment** (not a new standalone PR comment):

```
gh api repos/{owner}/{repo}/pulls/{pr_number}/reviews \
  --jq '.[] | select(.state == "CHANGES_REQUESTED" or .state == "COMMENTED") | {id, submitted_at}' \
  | # get the review ID

# Get the first comment on that review to reply to:
gh api repos/{owner}/{repo}/pulls/{pr_number}/reviews/{review_id}/comments \
  --jq '.[0].id'

# Post the reply:
gh api repos/{owner}/{repo}/pulls/comments/{comment_id}/replies \
  -f body="<fix-summary markdown>"
```

The reply body should contain:
- The resolution table from Step 3
- The overall verdict from Step 4
- Next recommended action:
  - Verdict ✅ → "Run `re-code-review` to publish an approval or merge the PR"
  - Verdict ⚠️ → "Outstanding warnings noted; run `re-code-review` if a fresh review is needed"
  - Verdict ❌ → "Blocking issues remain — fix these before merging"

---

## Step 6 — Terminal Summary

Print a concise terminal summary:

```
🔍 Review-Fix Summary — PR #<N>
──────────────────────────────────────
Commits since last review: <N>
Findings checked: <total>  ✅ <resolved>  ⚠️ <partial>  ❌ <unresolved>
──────────────────────────────────────
Verdict: <Ready to merge | Blocking issues resolved | Still blocked>
Reply posted to: <review_comment_url>
```

---

## Implementation Notes

- This skill only **verifies** — it does not fix code, submit new PR reviews, or merge
- Always reply to the *original* review comment thread; never create a new top-level comment
- If the PR author cannot submit `APPROVE` via `gh pr review` (GitHub limitation on own PRs),
  note it in the terminal output only — the reply comment is the authoritative record
- Use `gh api` for all GitHub operations; `git diff` for diff inspection
