# Code Review — PR #74 `feat(app): in-app GitHub Releases version checker` (round 1)

Reviewed against: base-developer + csharp-developer profile rules (global:code-review skill).
Scope: `feature/version-checker` @ `3e2f24c9` (5 commits, base `main`).

## Error (must fix before merge)

*None open.*

- ~~[GitHubUpdateChecker.cs / AboutPageViewModel.cs / UpdateCheckResult.cs] — Missing `<summary>`/`<param>`/`<returns>` on private helpers (`ParseRelease`, `FindInstallerAssetUrl`, `GetEntryAssemblyVersion`, `CheckForUpdatesAsync`, `OpenDownloadPage`, `Format`) and constructor params — csharp-developer §XML Documentation ("all members regardless of visibility")~~ — **fixed in `3e2f24c9`** before publishing this review.

## Warning (should fix, may block depending on team policy)

*None.*

## Suggestion (optional improvement, non-blocking)

- [GitHubUpdateCheckerTests.cs] — Test names use `Scenario_ExpectedOutcome` (e.g. `NewerRelease_ReportsUpdateAvailable`) rather than the profile's 3-part `MethodName_Scenario_ExpectedOutcome`. Repo precedent is mixed (`Dispose_CalledTwice`, `SaveThenLoad_Roundtrips_AllProperties`), and the class exercises a single method, so this is cosmetic. Non-blocking.
- [GitHubUpdateChecker.cs:50] — The 10 s `HttpClient.Timeout` is a magic literal in the constructor. Acceptable for a single call site; a named constant would match the style of `InstallerAssetSuffix` if this grows.

## Passed

- **Scope discipline ✓** — every hunk traces to the version-checker task, the orphaned-command removal it uncovered, or the user-requested gitignore housekeeping; all three are declared in the PR body. No drive-by reformats.
- **Breaking changes ✓** — PR body declares "none". The one public-surface removal (`MainWindowViewModel.CheckForUpdatesCommand`) is justified: repo-wide search shows zero XAML/code references (safe-delete rule satisfied); app ViewModels are not a published package contract.
- **Security ✓** — single hard-coded HTTPS GET to the GitHub API; no secrets, no deserialization of attacker-controlled types (`JsonDocument` read-only DOM with `TryGetProperty` guards), no sensitive values logged (status code + exception only). Browser launches use `UseShellExecute` with constant/API-provided GitHub URLs.
- **Reliability ✓** — checker never throws into the UI: HTTP errors, rate-limit 403, malformed JSON, bad tags and timeouts all map to `Failed` results; caller cancellation propagates correctly (`when (cancellationToken.IsCancellationRequested)` filter). 4-part assembly vs 3-part tag normalization handled explicitly.
- **NRT ✓** — signatures declare nullability accurately (`Version?`, `string?` on optional result members); no null-forgiving `!` in production code.
- **DI / lifecycle ✓** — `TryAddSingleton` consistent with other overridable Core services; container disposes the `IDisposable` checker (owned `HttpClient` released). Singleton `HttpClient` avoids socket exhaustion.
- **Threading ✓** — `ReactiveCommand.CreateFromTask` resumes on the Avalonia sync context; reactive properties only set from the command's continuation.
- **Tests ✓** — 28 new tests; happy path, boundary (tag format theories incl. null/empty/prefix/prerelease), and error paths (HTTP status, network exception, invalid JSON, cancellation) all covered with a stubbed `HttpMessageHandler` at the architectural boundary. FluentAssertions throughout, AAA structure, no branching in test bodies. 355/355 pass, Release `-warnaserror`, 0 warnings.
- **PR description ✓** — What/Why/Breaking (+Test plan) present; Why cites the backlog item and user impact.

## Verdict

**Approved.** No blocking findings. The two suggestions are cosmetic and may be ignored or deferred.
