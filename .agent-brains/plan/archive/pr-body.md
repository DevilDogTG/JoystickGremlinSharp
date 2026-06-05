## What changed

The `Check for Updates` flow is now a real in-app version check instead of a bare browser link. A new `JoystickGremlin.Core.Update` namespace provides `IUpdateChecker` / `UpdateCheckResult` and a `GitHubUpdateChecker` that queries the repository's latest GitHub Release, parses the `vX.Y.Z` tag (tolerating a `v`/`V` prefix and semver prerelease/build suffixes), and compares it against the running assembly version — normalized to three components so the 4-part assembly version (`12.1.0.0`) compares equal to its 3-part release tag. Network, rate-limit, and malformed-response failures never throw; they surface as `Failed` results with a human-readable message. When a newer release is found, the result carries the `*-Setup.msi` asset's direct download URL and the curated release notes body.

The About page gains an **Updates** section: a *Check for Updates* button, a status line (checking / up to date / update available / failed), and a *Download Update* button shown only when a newer version exists, opening the installer download (or release page) in the browser. `AboutPageViewModel` now receives `IUpdateChecker` and `ILogger` via DI.

Dead code removed along the way: `MainWindowViewModel.CheckForUpdatesCommand` had no XAML binding anywhere — the toolbar button it served was removed in an earlier UI pass, leaving the browser-open fallback orphaned.

Housekeeping: `.claude/settings.local.json` (per-developer Claude Code permission grants) is untracked and gitignored; shared `.claude/` config stays in the repo.

## Why

Closes the "In-app GitHub Releases version checker" backlog item (plan: `.agent-brains/plan/feature-version-checker.md`). Users previously had no way to know a newer release existed without manually visiting GitHub; now the About page answers it in one click, reusing the curated `RELEASE-NOTES.md` body that `publish.yml` attaches to every release. No auto-install — download stays an explicit browser action.

## Breaking changes

None. New Core surface is additive (`Update` namespace + `AddCoreServices` registration via `TryAddSingleton`). The removed `MainWindowViewModel.CheckForUpdatesCommand` was unreachable from any view. Settings schema, profile format, and installer are untouched.

## Test plan

- 355/355 unit tests pass (baseline 327 + 28 new), `dotnet build -c Release -warnaserror` clean, 0 warnings
- New coverage: tag-parsing accept/reject theories, version normalization, installer-asset and release-URL extraction, and all failure paths (HTTP error status, network exception, invalid JSON, caller cancellation) via a stubbed `HttpMessageHandler`
- Manual: check on About page against the live v12.1.0 release (current build reports up to date; a locally lowered version reports the update with working download link)
