# Plan: Auto-load (process → profile) Rework

**Status:** active
**Created:** 2026-05-28
**Branch:** feature/autoload-rework

## Goal
A dedicated "Auto-load" page where the user enables auto-load, adds mappings by picking a running
(windowed, game-flagged) process or a manual exe path, and assigns one of the app's created profiles
from a dropdown. Focus the mapped app → profile activates; switch away → pipeline stops but profile
stays loaded; refocus → resumes.

## Locked decisions
- Match on **executable name** (default, from picker), show captured path; manual mode = **full path**. **Drop regex.**
- Keep advanced toggles (`AutoStart`, `RemainActiveOnFocusLoss`).
- Focus loss: stop pipeline, keep profile loaded (already current behaviour).
- Process list: windowed apps only + best-effort game flag/sort.
- Profiles from `IProfileLibrary` dropdown, referenced by `FilePath`.
- Dedicated page (own nav entry), removed from Settings.

## Checklist
- [x] Core: `ProcessMatchType` enum + `MatchType`/`ExecutableName` on `ProcessProfileMapping`
- [x] Core: rework `ProcessProfileResolver` (name/path, drop regex)
- [x] Core: `GameHeuristics.IsLikelyGame`
- [x] Core: `IProcessEnumerator` + `RunningProcessInfo` + `NullProcessEnumerator` + DI
- [x] Tests: rewrite resolver tests; add `GameHeuristicsTests`; add backward-compat tests
- [x] Interop: `WindowsProcessEnumerator` + DI override
- [x] App: `ProcessPickerViewModel` + `ProcessPickerDialog` + `IProcessPickerDialogService`
- [x] App: `AutoLoadPageViewModel` + `AutoLoadPageView` + nav/DI wiring
- [x] App: rework `ProcessMappingViewModel` (pick process / browse exe / profile dropdown)
- [x] App: remove auto-load from `SettingsPageViewModel` + `SettingsPageView.axaml`
- [x] Build 0 warnings, all tests green (319/319), backward-compat verified

## Progress Log
- 2026-05-28: Plan approved; branch `feature/autoload-rework` created. Full design in
  `~/.claude/plans/elegant-strolling-neumann.md`.
- 2026-05-28: Implementation complete. Build clean (0 warnings), 319/319 tests passing
  (303 baseline + 16 net new: resolver name/path tests, GameHeuristics tests, two
  backward-compat tests that lock the no-`MatchType` JSON path). During implementation
  the original plan's "no migration needed" claim was empirically tightened: STJ keeps
  the C# property initializer for absent JSON fields, so the `MatchType` initializer
  was removed (default now = `ExecutablePath` = enum 0), which matches the legacy
  semantics. Locked by `ProcessProfileMappingCompatTests`. Awaiting manual E2E on
  Windows (focus-driven activation/deactivation/resume).
