## Code Review — PR #71 `feature/global-autoload` (round 1)

Reviewed against base-developer + csharp-developer profile rules (scope discipline,
breaking changes, security, quality, tests, PR description). Full diff vs `main`,
changed files read in full.

### Error (must fix before merge)

- `AutoLoadPageViewModel.cs` (SaveAsync) + `ProcessTriggerViewModel.cs` (ApplyToModel) —
  **In-place mutation of live trigger instances read by the monitor thread.**
  `RebuildRows` wraps the *live* `AppSettings.AutoLoadTriggers` instances; on save,
  `row.ApplyToModel()` writes through to those same instances *before* the list reference
  is swapped, while `ProcessMonitorService` may be enumerating them on a non-UI thread.
  Torn reads (e.g. `MatchType` flipped while `ExecutableName` still empty) can mis-fire or
  miss an auto-load. This contradicts the replace-don't-mutate convention documented on
  `AppSettings.AutoLoadTriggers` itself. → Snapshot to **new** `AutoLoadTrigger` instances
  on save; never write through to shared models.

### Warning (should fix)

- `AutoLoadPageViewModel.cs` (RefreshLegacyDetectionAsync / OnLibraryChanged) —
  `DetectAsync` does synchronous whole-library file I/O on the UI thread on every
  `LibraryChanged` (profile add/rename/delete). → Move detection (and the manual
  migration) onto a background thread.
- `SettingsService.cs` (SaveAsync) — no serialization gate; the immediate
  `EnableAutoLoading` save and the 800 ms debounced trigger save can overlap →
  interleaved `File.Create` writes can corrupt `settings.json`. → Gate with a
  `SemaphoreSlim` (same pattern as `ProfileLibrary`).
- `README.md` architecture table — still lists deleted type `ProcessTrigger` as a Core
  domain type. → Replace with `AutoLoadTrigger`.

### Suggestion (non-blocking)

- `AutoLoadTriggerMigrator.cs` `_writeOptions` comment overclaims "matches
  ProfileRepository's output formatting" — only 2-space indentation is matched (remaining
  content is preserved verbatim via the JsonNode round-trip, so this is cosmetic). →
  Reword.
- Tests: the strip-failure path is only exercised via broken JSON; no test covers an
  `IOException`/read-only file during pass 3 (the window the settings-first ordering
  exists to protect). → Add a read-only-file migrator test.

### Passed

- Scope discipline ✓ (every hunk traces to the trigger-ownership move; no drive-by edits)
- Breaking changes ✓ (declared "none" with migration rationale; public-surface removals
  are the task itself)
- Security surface ✓ (no secrets/SQL/deserialization-of-untrusted-input; JSON parsing is
  per-file fail-isolated)
- Migrator ordering & idempotency ✓ (settings-first, dedupe-on-retry, per-file failure
  isolation — all covered by tests)
- STJ absent-field defaults ✓ (`IsEnabled`/`AutoStart = true` initializers reproduce
  legacy v11 semantics; enum-as-string round-trip tested; legacy `autoLoadTriggers`
  property tolerated on load, tested)
- XAML compiled bindings ✓ (`x:DataType` on both templates; page-VM command lookups and
  `CommandParameter` signatures line up)
- No dead references to removed types in compiled code ✓
- PR description ✓ (What/Why/Breaking present and substantive)
- XML docs ✓ per repo rule (public surface documented; private-member docs are not
  required by AGENTS.md and existing convention)

**Verdict: Changes requested** — finding #1 is blocking; warnings should land in the same
fix round.
