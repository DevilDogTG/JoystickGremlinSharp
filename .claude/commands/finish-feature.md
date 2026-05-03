---
name: finish-feature
description: >
  Complete a feature: commit, push, raise PR, and perform code review all in one workflow.
  Use this when you're ready to finalize and ship a feature branch. Runs all steps
  autonomously and returns a summary of the PR + review findings.
---

# Finish Feature Workflow

Automate the feature completion process: commit → push → PR → code review.

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

### Step 5 — Fix Follow-up & Summary

1. If review findings are fixed in the same workflow:
   - push the fix commit(s)
   - update the PR review status appropriately on GitHub
   - post a PR comment summarizing what was fixed and the final result
2. If findings remain unfixed:
   - leave the blocking review status in place
   - post a PR comment summarizing the outstanding issues and required next action

Print a completion summary:
- PR number and URL
- Review verdict (Approve / Request Changes / Comment)
- Top findings (CRITICAL / WARNING count)
- Next action (merge, make changes, etc.)

## Implementation Notes

- All steps are autonomous; no user input required once started
- If any step fails, report error and stop (don't swallow failures)
- Use `git` CLI for git operations, `gh` CLI for GitHub
- Use `gh pr review` to publish the review result to GitHub and `gh pr comment` for the fix-summary follow-up
- Assume SSH-based git URLs (no https auth needed)
- Log all operations at INFO level for audit trail
