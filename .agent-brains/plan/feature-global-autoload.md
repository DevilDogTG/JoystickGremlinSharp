# Plan: Global Auto-Load Trigger Store

**Status:** active
**Created:** 2026-06-05
**Branch:** feature/global-autoload

## Goal
Move auto-load process triggers out of individual profile files into a single global
trigger store (persisted in `settings.json`), managed from one flat settings UI page,
with lossless auto-migration from v11/v12 profile-embedded triggers. Ships as v12.1.0
(minor — migration is automatic, no user-visible break).

## Design Decisions

- **Storage**: new `AppSettings.AutoLoadTriggers` list persisted via the existing
  `SettingsService` → `%APPDATA%\JoystickGremlinSharp\settings.json`. No new
  persistence layer.
- **Profile reference**: each global trigger carries a `ProfilePath` (absolute path,
  same convention as `AppSettings.ActiveProfilePath`). This deliberately resurrects
  the v10.x back-pointer dropped in v11 — the inverse of PR #66. GUID-based reference
  rejected: profiles are tracked by path everywhere else (`ProfileEntry.FilePath`,
  `ActiveProfilePath`), and `ProfileEntry` does not expose `Profile.Id`.
- **Trigger model**: introduce `AutoLoadTrigger` in `Core.Configuration` composing the
  existing trigger fields + `ProfilePath` (or re-add `ProfilePath` to `ProcessTrigger`
  — decide at implementation, prefer whichever keeps profile JSON schema untouched
  for the migration reader).
- **Migration**: one-time, idempotent, at startup before the first library scan:
  for every profile file containing a non-empty `AutoLoadTriggers` array →
  append to global store (preserving library scan order so resolver priority is
  unchanged) → strip the array from the profile JSON → save settings once at the end.
  Re-running on already-migrated profiles is a no-op.
- **Priority semantics preserved**: v11 priority = library scan order, then trigger
  declaration order. Post-migration the flat global list order reproduces exactly
  that sequence; thereafter the user controls order directly via ↑/↓ in the UI.

## Checklist

- [x] **1. Model + settings**
  - [x] `AutoLoadTrigger` type with `ProfilePath` + existing trigger fields
        (`src/JoystickGremlin.Core/Configuration/`)
  - [x] `AppSettings.AutoLoadTriggers : List<AutoLoadTrigger>` (`AppSettings.cs`)
- [x] **2. Migration**
  - [x] `IAutoLoadTriggerMigrator` / `AutoLoadTriggerMigrator` service — reads
        per-profile triggers (reuse the lightweight `TriggersOnlyDto` read), lifts
        into settings, strips profile JSON, idempotent. Exposes:
        - `DetectAsync()` → list of profile files still carrying embedded triggers
        - `MigrateAsync()` → performs lift+strip, returns summary (migrated count,
          failures)
  - [x] Wire into app startup before first scan / monitor start (automatic pass)
  - [x] Registered in DI so the auto-load page can invoke it manually too
- [x] **3. Resolver**
  - [x] `ProcessProfileResolver.Resolve(string exePath, IEnumerable<AutoLoadTrigger>)`
        → `ProcessTriggerMatch(AutoLoadTrigger)`; first-enabled-match-wins and the
        normalization rules stay identical
        (`src/JoystickGremlin.Core/ProcessMonitor/ProcessProfileResolver.cs`)
- [x] **4. Runtime**
  - [x] `ProcessMonitorService` sources triggers from `ISettingsService` instead of
        `IProfileLibrary.Entries`; loads matched profile by `trigger.ProfilePath`
        (`src/JoystickGremlin.App/Services/ProcessMonitorService.cs`)
- [x] **5. Library cleanup**
  - [x] Remove `ProfileEntry.AutoLoadTriggers` snapshot, `ReadTriggers`,
        `TriggersOnlyDto` from `ProfileLibrary` (migrator keeps its own copy of the
        lightweight read)
  - [x] Remove `Profile.AutoLoadTriggers` (profile JSON schema no longer carries it;
        unknown-property tolerance covers old files post-strip)
- [x] **6. UI**
  - [x] `AutoLoadPageViewModel`: flat `ObservableCollection<ProcessTriggerViewModel>`
        (no per-profile grouping); add profile picker per row (dropdown of library
        entries); keep 800 ms debounced save, now targeting settings.json only
  - [x] Delete `ProfileTriggersGroupViewModel`
  - [x] `AutoLoadPageView.axaml`: single table — Profile | Application | On |
        AutoStart | Stay Active | ↑↓🗑; keep global Enabled checkbox
  - [x] **Manual migration banner**: on page load (and on `LibraryChanged`), run
        `DetectAsync()`; if any profile still carries embedded triggers (file copied
        in after startup, or startup migration failed on a locked file), show an
        info banner: "N profile(s) contain legacy embedded auto-load triggers" with
        a **Migrate now** button → `MigrateAsync()` → refresh trigger list + banner;
        surface per-file failures in the banner instead of failing silently
- [x] **7. Tests** (baseline 315, 0 warnings)
  - [x] Resolver tests rewritten for flat trigger list (keep all match-semantics cases)
  - [x] Migrator tests: lifts + strips + idempotent + preserves order + broken JSON
        + `DetectAsync` finds late-added legacy profiles + failure summary reported
  - [x] `ProfileLibraryTests`: drop trigger-extraction cases
  - [x] Settings round-trip with `AutoLoadTriggers`
- [x] **8. Docs + release**
  - [x] README auto-load section; note migration behavior in CHANGELOG/release notes
        (not BREAKING-CHANGES.md — lossless)
  - [x] Bump 12.0.1 → 12.1.0
  - [x] Update `.agent-brains/memory/overview.md` Auto-Load section
  - [x] Note: this removes the rationale for the backlog item
        "`ProfileLibrary.ScanCore` async + parallel JSON read" (the per-file trigger
        read disappears) — close it when this lands

## Risks / Open Points

- Profile rename/move via the UI breaks `ProfilePath` references — same limitation
  v10.x had; resolver simply won't match until the user re-points the trigger.
  Acceptable for v12.1.0; could add path-fixup later.
- Migration touches every profile file once — must not corrupt profiles on partial
  failure (write trigger to settings only after the strip-save of that profile
  succeeds, or vice versa; make order deliberate and tested).

## Progress Log
_2026-06-05 — Plan created from session-start change request; code surface mapped._
_2026-06-05 — Implementation complete (steps 1–8). All 327 tests pass, Release build
with `-warnaserror` clean. Notes vs. plan:_
- _`AutoLoadTrigger`/`ProcessMatchType` live in `Core.ProcessMonitor` (not Configuration)._
- _Resolver returns `AutoLoadTrigger?` directly; `ProcessTriggerMatch` record deleted._
- _Migration safety order refined: settings saved BEFORE profile-strip + dedup on
  retry (no data loss, no duplicates)._
- _`AppSettings.AutoLoadTriggers` is replace-only (atomic reference swap) because the
  process monitor enumerates it off the UI thread._
- _Test count 315 → 327 (13 new migrator tests, 2 settings round-trip, 1 legacy-tolerance
  repo test; minus removed per-profile trigger tests)._
_2026-06-05 — Shipped for review: 5 atomic commits + PR #71 (ready, base main). User-found
bug fixed (ComboBox selection lost on page navigation — SelectedItem/ItemsSource attach
race; commit ca5fd43e). Code review round 1 posted to PR: 1 error (rows mutated live
trigger instances read by the monitor thread — fixed via ToTrigger() snapshots), 3
warnings (UI-thread file I/O on LibraryChanged; unsynchronized settings.json writes;
stale README type) + 2 suggestions — all resolved in 47958672. 328 tests, -warnaserror
clean._
_Remaining: review approval + merge (rebase-merge, delete branch) via workflow-end._
