# Plan: In-App GitHub Releases Version Checker

**Status:** done — merged 2026-06-05 (PR #74, rebase-merge `ddddecc3`)
**Created:** 2026-06-05
**Branch:** feature/version-checker

## Goal
The `Check for Updates` toolbar button performs an in-app check against GitHub Releases (latest release vs. current version via semver), shows the result with release notes and a download link — no auto-install.

## Design Notes
- Repo: `DevilDogTG/JoystickGremlinSharp`; API: `GET https://api.github.com/repos/DevilDogTG/JoystickGremlinSharp/releases/latest` (unauthenticated, 60 req/h — fine for on-demand checks).
- Current version source: assembly informational version (fed from `version.json` at build time) — verify how the About page reads it and reuse.
- Release tag format `vX.Y.Z` → strip `v`, compare semver. Release body is the curated `RELEASE-NOTES.md` (since v12.1.0) — suitable for display.
- Service lives in Core behind an interface (e.g. `IUpdateChecker`) with HTTP injectable for tests; concrete `HttpClient` wiring at composition root.
- UX: button click → checking state → dialog/banner: "Up to date" or "vX.Y.Z available" + notes excerpt + button opening the release page (or `*-Setup.msi` asset URL) in browser.
- Out of scope: auto-download, auto-install, background polling. (A startup check could be a later opt-in setting — not this PR.)

## Checklist
- [x] `IUpdateChecker` + `UpdateCheckResult` in Core (semver compare, latest-release parsing)
- [x] HTTP implementation (GitHub Releases API, User-Agent header, 10s timeout, graceful offline/rate-limit failure)
- [x] Wire into composition root (`AddCoreServices` → `GitHubUpdateChecker`)
- [x] Result UI: "Updates" section on the About page — check button, status line, conditional Download button
- [x] Tests: tag parsing edge cases, version normalization, metadata extraction, failure paths (stubbed HTTP) — 355 total, 0 warnings
- [x] Code review — PR #74 round 1 (global:code-review): Approved, 0 blocking
      (XML-doc gaps fixed pre-publish in `3e2f24c9`; 2 cosmetic suggestions left open)

## Progress Log
- 2026-06-05 — Plan created (session: feature discussion → version checker confirmed as active work).
- 2026-06-05 — Implemented. Design deviation from plan: the toolbar `Check for Updates`
  button no longer existed — `MainWindowViewModel.CheckForUpdatesCommand` was orphaned
  (no XAML binding) and has been REMOVED; the update check lives on the About page
  instead (`AboutPageViewModel` + Updates section in `AboutPageView.axaml`).
  Tests 327 → 355. Awaiting code review / PR.
- 2026-06-05 — PR #74 raised (pr-workflow skill) and reviewed (global:code-review).
  Verdict: Approved. Review published: PR #74 comment (round 1). Awaiting merge.
- 2026-06-05 — Round-1 cosmetic suggestions also fixed on request (test naming →
  3-part convention; RequestTimeout constant) in `1b27bc86`. PR #74 rebase-merged
  to main (`ddddecc3`), branch deleted. Ships in the next release (unreleased on
  main for now; version.json still 12.1.0). Plan archived.
