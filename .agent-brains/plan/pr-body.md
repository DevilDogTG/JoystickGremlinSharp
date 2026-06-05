## What changed

Auto-load process triggers move from being embedded inside each profile JSON (the v11
design) to a single global list stored in `settings.json` (`AppSettings.AutoLoadTriggers`).
Each trigger now references its target profile by file path; triggers are evaluated in
list order and the first enabled match wins. The Auto-load page becomes one flat table
with a per-row profile picker instead of per-profile expander groups. A new
`AutoLoadTriggerMigrator` lifts legacy embedded triggers into the global list — it runs
automatically at startup and is also exposed as a "Migrate now" banner on the Auto-load
page for profiles copied in from older installations after startup. Migration is
idempotent and ordered for safety: the settings file is saved *before* profile files are
stripped, and re-runs dedupe, so a partial failure can neither lose nor duplicate
triggers. With triggers gone from profile files, `ProfileLibrary` no longer does a
per-file JSON read during scans, and `ProcessMonitorService` resolves directly against
settings (the trigger list is replace-only — atomic reference swap — because the monitor
enumerates it off the UI thread). Includes a fix for the profile dropdown losing its
displayed selection after page navigation (ComboBox SelectedItem/ItemsSource attach-order
race).

## Why

Per-profile trigger ownership made the trigger overview fragmented (one expander per
profile), forced a lightweight JSON read of every profile on each library scan, and
coupled trigger priority to library scan order instead of user intent. A central list
restores the at-a-glance management of the v10 design while keeping the v11+ per-trigger
options (AutoStart, Stay Active, match modes) — and unlike the v11.0 break, this change
migrates user data automatically and losslessly, which is why it ships as a minor bump
(v12.1.0).

## Breaking changes

None. v11.x/v12.0 profile-embedded triggers are migrated automatically at startup
(idempotent, lossless); profile files shared from older versions are detected and
migrated on demand from the Auto-load page. v10.x global mappings remain unmigrated, as
since v11.0.

## Test plan

- 327 unit tests pass (315 baseline + new migrator, settings round-trip, and
  legacy-tolerance coverage), `dotnet build -c Release -warnaserror` clean
- Manual: trigger fires and loads referenced profile; legacy profile copied into the
  library shows the migration banner; "Migrate now" imports and strips; selection
  survives page navigation (user-verified)
