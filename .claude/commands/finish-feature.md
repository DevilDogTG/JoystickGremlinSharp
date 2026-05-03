---
name: finish-feature
description: >
  Complete a feature: commit, push, raise PR, and perform code review all in one workflow.
  Use this when you're ready to finalize and ship a feature branch. Runs steps up to code
  review autonomously, then waits for user confirmation before finishing.
---

# Finish Feature Workflow

Automate the feature completion process: commit → push → PR → code review → **wait for user confirmation** → finish.

> **IMPORTANT**: After the code review is posted to the PR, **always stop and ask the user
> for confirmation** before taking any further action (merging, marking complete, etc.).
> Do not autonomously complete the workflow end-to-end without user sign-off.

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

### Step 5 — Pause and Confirm

After the review is posted:
1. **Stop and ask the user** whether to proceed (fix issues, merge, or leave as-is)
2. Do NOT autonomously fix findings, merge, or mark complete without explicit user instruction
3. Present a brief summary: PR URL, verdict, finding counts, suggested next action

### Step 6 — Fix Follow-up (only if user confirms)

If the user confirms fixes should be applied:
1. Fix the reported issues, push the fix commit(s)
2. Update the PR review status appropriately on GitHub
3. Post a **reply to the original review comment** (not a new top-level PR comment) summarizing
   what was fixed and the final status — use `gh api` to post a reply to the review thread:
   ```
   gh api repos/{owner}/{repo}/pulls/{pr}/reviews/{review_id}/comments
   ```
   or use `gh pr comment --reply-to <comment_id>` if the review body has a thread ID

If findings remain unfixed:
- Leave the blocking review status in place
- Post a **reply to the original review comment** summarizing outstanding issues and required next action

Print a final summary:
- PR number and URL
- Review verdict (Approve / Request Changes / Comment)
- Top findings (CRITICAL / WARNING count)
- Next action (merge, make changes, etc.)

## Implementation Notes

- Steps 1–4 (commit → push → PR → review) run autonomously; **Step 5 always pauses for user input**
- If any step fails, report error and stop (don't swallow failures)
- Use `git` CLI for git operations, `gh` CLI for GitHub
- Use `gh pr review` to publish the review result to GitHub
- Fix-summary feedback must be a **reply to the review comment**, not a new standalone PR comment
- Assume SSH-based git URLs (no https auth needed)
- Log all operations at INFO level for audit trail
