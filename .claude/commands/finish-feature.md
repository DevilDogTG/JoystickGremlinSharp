---
name: finish-feature
description: >
  Complete a feature: commit, push, raise PR, and perform code review all in one workflow.
  Use this when you're ready to finalize and ship a feature branch. Runs steps up to code
  review autonomously, then stops and waits for the user to choose the next step.
---

# Finish Feature Workflow

Automate the feature completion process: commit → push → PR → code review → **stop and wait for user**.

> **IMPORTANT**: After the code review is posted to the PR, **the workflow ends**.
> Do NOT autonomously fix findings, merge, or take any further action.
> Present the review summary and wait — the user decides what to do next
> (fix issues, request re-review, merge, or leave as-is).
> Use the **review-fix** skill to verify fixes, or **re-code-review** to run a full re-review.

## Prerequisites

- Working directory is clean (all changes staged/committed or in `.gitignore`)
- Feature branch is checked out (not main/develop)
- Commit message is prepared or will be auto-generated from staged changes

## Workflow Steps

### Step 1 — Prepare & Commit

1. Check current branch name (must not be `main` or `develop`)
2. Stage any uncommitted changes
3. Prompt user for commit message if needed
4. Commit with trailer: `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`

### Step 2 — Push to Origin

1. Push feature branch to `origin/<branch-name>` with upstream tracking
2. Confirm push succeeded

### Step 3 — Create PR

1. Set default repo via `gh repo set-default` if needed
2. Create PR with:
   - **base**: `main`
   - **head**: current feature branch
   - **title**: auto-derived from commit message (first line)
   - **body**: auto-generated summary from commit body + test results
3. Extract PR number from output

### Step 4 — Code Review

1. Invoke the **code-review** skill on the feature branch
2. Generate structured review with findings summary + top 3 fixes
3. Post the review to the PR on GitHub:
   - use **Approve** when there are no blocking findings
   - use **Comment** when findings are informational/non-blocking
   - use **Request changes** when there are blocking issues that must be fixed before merge
4. Confirm the review appears on the PR with the expected GitHub review status
5. Output the same review results inline in the terminal summary

### Step 5 — Stop and Present Summary

After the review is posted, **the workflow is complete**. Output a final summary and stop:

```
✅ Finish-feature complete
──────────────────────────────
PR:      #<number> — <title>
URL:     <pr_url>
Verdict: <Approve | Comment | Request Changes>
Findings: <N> CRITICAL  <N> WARNING  <N> STYLE
──────────────────────────────
Next steps (your call):
  • Fix issues → run `review-fix` to verify fixes resolved the review
  • Re-review  → run `re-code-review` for a full review after applying fixes
  • Merge      → merge the PR if the review approved it
```

Do NOT take any further autonomous action after printing this summary.

## Implementation Notes

- Steps 1–4 (commit → push → PR → review) run autonomously; **Step 5 always ends the workflow**
- If any step fails, report error and stop (don't swallow failures)
- Use `git` CLI for git operations, `gh` CLI for GitHub
- Use `gh pr review` to publish the review result to GitHub
- Fix-summary feedback is handled by the **review-fix** skill, not by this workflow
- Assume SSH-based git URLs (no https auth needed)
- Log all operations at INFO level for audit trail
