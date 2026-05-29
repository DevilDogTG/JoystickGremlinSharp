# Plan: Cleanup pass — drop dotnet-ci.yml + strip HidHide dead code

**Status:** active
**Created:** 2026-05-29
**Branch:** chore/cleanup-hidhide-and-ci

## Goal

Remove dead code revealed by the post-v11.0 audit so the codebase reflects what
actually ships: per-device HidHide hiding was never wired to UI; the .NET CI
workflow duplicates what `publish.yml` does at tag time and is being replaced by
PR-review trust.

## Scope (confirmed with user)

1. Delete `.github/workflows/dotnet-ci.yml` — no dependents, no badge, no doc.
2. Trim `HidHideManager` to the surface that is actually called:
   - Keep: `InitializeAsync` (whitelist + crash-recovery), `Dispose` (whitelist removal).
   - Delete: `ApplyAsync`, `RevertAsync`, `RefreshAsync`, `OnPipelineStarted`,
     `OnPipelineStopped`, pipeline event subscriptions, `StatusChanged` event,
     `Status`, `IsApplied`, `LastError` properties.
   - Keep `IHidHideManager` name + shape (user preference).
3. Remove dead `AppSettings` fields: `EnableHidHide`, `AutoHideOnPipelineRun`,
   `HiddenDeviceInstanceIds`. System.Text.Json ignores unknown keys, so existing
   `settings.json` files keep loading.
4. Trim `AGENTS.md` HidHide section (lines 393-472) — it documents a settings UI
   that doesn't exist.
5. Update `HidHideManagerTests.cs` — delete tests covering removed surface.

## Out of scope

- `publish.yml` build+test (still useful at tag time).
- Class/interface rename (user kept `IHidHideManager`).
- `DirectInputNative.cs` `*_Unused` stubs — intentional COM vtable ordering.

## Checklist

- [x] Create branch `chore/cleanup-hidhide-and-ci`
- [x] Delete `.github/workflows/dotnet-ci.yml`
- [x] Trim `IHidHideManager.cs` interface
- [x] Trim `HidHideManager.cs` implementation
- [x] Remove four `AppSettings` properties (incl. `HiddenDevices`)
- [x] Delete `HidHideStatus.cs` and `HiddenDeviceEntry.cs` (dead types)
- [x] Trim `AGENTS.md` HidHide section
- [x] Trim `HidHideManagerTests.cs`
- [x] `dotnet build -warnaserror` clean (0 warnings)
- [x] `dotnet test` passes (315/315)
- [x] Update `overview.md` and `backlog.md`
- [x] Commit + push + open PR (#69)
- [x] Code review round 1 — 5 Warnings, all addressed in `47abbf78`
- [x] Code review round 2 — Approved with 2 cosmetic Suggestions only (`3032cb2b`)

## Progress Log

- 2026-05-29: Branch created. Deleted `dotnet-ci.yml`. Trimmed `IHidHideManager` to just
  `InitializeAsync` + `Dispose`. `HidHideManager` simplified — no longer takes
  `IEventPipeline` or `ISettingsService`; just whitelists own exe on startup and
  removes on dispose. Deleted `HidHideStatus.cs` (enum) and `HiddenDeviceEntry.cs`.
  Removed `EnableHidHide`/`AutoHideOnPipelineRun`/`HiddenDeviceInstanceIds`/`HiddenDevices`
  from `AppSettings`. Tests trimmed from 30 to 5 cases (Init/Dispose only). Build clean.
  AGENTS.md HidHide section rewritten to match shipping reality. PR #69 opened.
- 2026-05-29 (review round 1): `global:code-review` surfaced 5 Warnings (3× missing
  braces, constructor missing `<summary>`, PR body missing Breaking-changes section).
  All addressed in commit `47abbf78` + `gh pr edit` for PR body.
- 2026-05-29 (review round 2): Applied 3 non-blocking suggestions in commit `3032cb2b`:
  primary-constructor refactor, dropped unused `CancellationToken` from
  `InitializeAsync`, added `Dispose_CalledTwice_RemovesWhitelistEntryOnlyOnce` test
  to guard the `_disposed` idempotency flag. Re-review: **Approved**. Test count
  315/315. PR #69 ready to merge.

## Files to modify

- `.github/workflows/dotnet-ci.yml` (delete)
- `src/JoystickGremlin.Core/HidHide/IHidHideManager.cs`
- `src/JoystickGremlin.Core/HidHide/HidHideManager.cs`
- `src/JoystickGremlin.Core/Configuration/AppSettings.cs`
- `tests/JoystickGremlin.Core.Tests/HidHide/HidHideManagerTests.cs`
- `AGENTS.md`
- `.agent-brains/memory/overview.md`
- `.agent-brains/plan/backlog.md`

## Progress Log

_Updated as steps complete._
