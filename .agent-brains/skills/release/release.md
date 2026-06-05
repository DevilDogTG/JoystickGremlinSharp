---
id: release
name: Release (JoystickGremlinSharp)
version: 1.0
compatibility: [claude, gemini, copilot, cursor]
---

# Skill: Release (JoystickGremlinSharp)

Cut a versioned release of JoystickGremlinSharp by driving the repo's release
automation: a `release/vX.Y.Z` PR into `main` triggers `tag.yml` (tags from
`version.json`), which triggers `publish.yml` (build + test + sign MSI + GitHub
Release with notes from the committed `RELEASE-NOTES.md`).

## Context

Use when `main` is at a releasable state. This skill REPLACES the generic
`gh-release` flow for this repo — never tag manually here: `tag.yml` owns tagging
(a manual tag races it) and `publish.yml` owns release creation (a manual
`gh release create` collides with it).

## Procedure

1. **Preflight**
   ```bash
   git checkout main && git pull --rebase
   git describe --tags --abbrev=0        # last released tag
   ```
   Working tree must be clean; no open `release/*` PR may exist.

2. **Analyze changes since the last tag**
   ```bash
   git log <last-tag>..main --oneline
   gh pr list --state merged --base main --search "merged:><last-tag-date>"
   ```
   Classify by Conventional Commit prefix:
   - any `!` / BREAKING-CHANGES.md touched / breaking PR declaration → **major**
   - else any `feat:` → **minor**
   - else (`fix:` / `chore:` / `docs:` / `style:` / `refactor:` / `perf:` / `test:`) → **patch**

   Also read `version.json`: if it was already bumped ahead of the last tag during a
   feature branch (this repo's convention), the recommended version is normally that
   value — validate it against the classification above and flag any mismatch.

3. **Recommend and confirm with the user** — present the recommended bump
   (major | minor | patch) WITH rationale (the classification evidence) and the
   resulting `vX.Y.Z`. Do not proceed without explicit confirmation.

4. **Align `version.json`** — if it does not already equal the confirmed version,
   update it.

5. **Write `RELEASE-NOTES.md`** (repo root — `publish.yml` publishes this file
   verbatim as the GitHub Release body):
   ```markdown
   ## What's new
   - <feature summaries from merged PRs — user-facing wording, not commit subjects>

   ## Bug fixes
   - ...

   ## Breaking changes
   <explicit statement — "none" if non-breaking>

   ## Migration guide
   <only if breaking changes exist or automatic migrations run>
   ```
   Cover every merged PR since the last tag. Link PR numbers (`#NN`).

6. **Open the release PR**
   ```bash
   git checkout -b release/vX.Y.Z
   git add version.json RELEASE-NOTES.md
   git commit -m "chore(release): prepare vX.Y.Z"
   git push -u origin release/vX.Y.Z
   gh pr create --base main --title "chore(release): vX.Y.Z" --body-file RELEASE-NOTES.md
   ```

7. **Merge (confirm with user first)**
   ```bash
   gh pr merge --rebase --delete-branch
   ```
   The head branch MUST keep the `release/` prefix — `tag.yml` keys on it.

8. **Watch the automation chain**
   ```bash
   gh run list --workflow tag.yml --limit 1
   gh run list --workflow publish.yml --limit 1
   gh run watch <publish-run-id>
   ```

9. **Verify the release**
   ```bash
   git ls-remote --tags origin | grep vX.Y.Z
   gh release view vX.Y.Z          # title, curated notes body, assets
   ```
   The release must carry `JoystickGremlinSharp-X.Y.Z-Setup.msi` and the body must
   match `RELEASE-NOTES.md` (not an auto-generated PR list).

## Validation

- [ ] Bump type confirmed by user with rationale shown
- [ ] `version.json` equals the released version at the merge commit
- [ ] Release notes cover all merged PRs since the previous tag
- [ ] `tag.yml` and `publish.yml` runs green
- [ ] `gh release view` shows curated notes + signed `*-Setup.msi` asset
