# Project Overview

## JoystickGremlinSharp — C# rewrite of JoystickGremlin

## Key Decisions

- **ADR-0001** (2026-05-25): `asInvoker` manifest + on-demand UAC for HidHide CLI subprocesses.
  See `.agent-brains/decisions/ADR-0001-on-demand-uac-for-hidhide-cli.md`.
- **ADR-0002** (2026-05-28): Replace Velopack with WiX v6 MSI installer (`perMachine`, WixUI_Mondo).
  Rationale: Velopack installs per-user (HKCU), causing app to be invisible in Windows Installed Apps.
  WiX 6.0.2 chosen over WiX 7.0.0 (7.0.0 introduced commercial maintenance fee).


**Branch model**: main-first, tag-based releases. Feature branches → rebase-merge PR → main.

**Test baseline**: 327 tests, 0 warnings (as of feature/global-autoload, 2026-06-05 — +12 net over the 315 PR #69 baseline: new migrator + settings round-trip coverage, minus deleted per-profile trigger tests).

**Current version**: v12.1.0 — released 2026-06-05 (global auto-load rework, PR #71; signed MSI on GitHub Releases). Previous breaking change at v11.0 — see `BREAKING-CHANGES.md` and Auto-Load section below.

**Release flow (since v12.1.0)**: use the workspace `release` skill — analyze + recommend bump → confirm → `RELEASE-NOTES.md` + `release/vX.Y.Z` PR → merge → `tag.yml` tags from `version.json` → `publish.yml` builds/signs MSI and publishes the release with the curated notes (`body_path`). NEVER tag manually or `gh release create` in this repo.

**Recent merges (2026-05-29)**:
- PR #69 merged via rebase-merge — dropped `dotnet-ci.yml`, stripped HidHide Apply/Revert dead code, primary-ctor refactor.

**README sync**: refreshed for v11.0 on 2026-05-29 (commit `8b0f32cc`) — now includes a feature-comparison table against the original Python JoystickGremlin and documents the v11 storage path consolidation.

---

## Installer Architecture (as of v10.7.0+)

- **Technology**: WiX SDK 6.0.2 MSI (`installer/JoystickGremlinSharp.wixproj` + `installer/Package.wxs`)
- **Scope**: `perMachine` / HKLM — visible in Settings > Apps and Control Panel > Programs and Features
- **Wizard**: `WixUI_Mondo` — Welcome → License → Setup Type → (Custom: path + feature tree) → Install
- **Shortcuts**: Start Menu (always); Desktop (Level=1, on by default, deselectable in Custom mode)
- **Upgrade**: `MajorUpgrade` with stable `UpgradeCode={3BE7219A-DAE0-41D4-BDB3-E0530808F9C3}`
- **CI**: `publish.yml` runs `dotnet publish` → `dotnet build .wixproj` → sign MSI → release as `*-Setup.msi`
- **In-app updates**: Removed (Velopack). `Check for Updates` toolbar button opens GitHub Releases in browser.
  Full semver version checker planned for future release (see backlog).

## Auto-Load Triggers (v12.1+, global store)

- **Global ownership** (v12.1 reversed the v11 per-profile design): triggers live in
  `AppSettings.AutoLoadTriggers` (`settings.json`), each referencing its target
  profile via `ProfilePath` (absolute path). Evaluated in list order, first enabled
  match wins.
- **Types**: `Core.ProcessMonitor` — `AutoLoadTrigger`, `ProcessMatchType`,
  `ProcessProfileResolver.Resolve(string, IEnumerable<AutoLoadTrigger>)` returns the
  matched trigger directly (the `ProcessTriggerMatch` wrapper is gone).
- **Migration**: `AutoLoadTriggerMigrator` lifts v11.x/v12.0 profile-embedded
  triggers — automatic at startup (`MainWindowViewModel.InitializeAsync`) + manual
  "Migrate now" banner on the Auto-load page for profiles copied in later.
  Safety order: settings saved BEFORE stripping profile files; dedup makes retries
  duplicate-free. Idempotent.
- **Thread-safety convention**: `AppSettings.AutoLoadTriggers` must be REPLACED
  (reference swap), never mutated in place — `ProcessMonitorService` enumerates it
  from a non-UI thread.
- **Storage paths** (consolidated under `%APPDATA%\JoystickGremlinSharp\`):
  - Profiles: `%APPDATA%\JoystickGremlinSharp\profiles\`
  - Settings: `%APPDATA%\JoystickGremlinSharp\settings.json`
  - Logs: `%LOCALAPPDATA%\JoystickGremlinSharp\logs\`
- **Global kill-switch**: `AppSettings.EnableAutoLoading` still gates the whole
  feature.
- **Known limitation**: renaming/moving a profile breaks `ProfilePath` references
  (trigger row shows ⚠ until re-pointed) — same as the v10.x design.
- **History**: v10.x global mapping → v11.0 per-profile (breaking, no migration,
  see `BREAKING-CHANGES.md`) → v12.1 global again (auto-migrated, lossless).

## Native Library Lifecycle Trap (recorded 2026-05-29)

- Bundled native DLLs that write debug logs to CWD will crash non-admin
  installed-build launches because CWD is `C:\Program Files\<app>\` or
  `C:\Windows\system32`, neither user-writable.
- Mitigation pattern: relocate `Environment.CurrentDirectory` to a per-user
  folder around the native `init()` call, restore in `finally`.
- Reference: `src/JoystickGremlin.Interop/Dill/DillDeviceManager.InitializeNative`
  (redirects to `%LOCALAPPDATA%\JoystickGremlinSharp\dill\`).
- Detail: `.agent-brains/memory/native-lib-cwd-trap.md`.

## HidHide Integration

HidHide is an optional device-hiding driver by Nefarius. Our integration is a **thin whitelist manager** (cleaned up 2026-05-29 — the original Apply/Revert event-pipeline code never got a settings UI and was removed):
- **Auto-whitelist**: `HidHideManager.InitializeAsync()` adds own exe to the bypass-list at startup so it can see devices HidHide may be hiding.
- **Whitelist cleanup**: `HidHideManager.Dispose()` removes own exe on clean exit.
- **On-demand UAC**: App runs as `asInvoker` (no forced admin). If CLI write needs elevation, Windows UAC prompt appears just for that subprocess. User can decline safely.
- **Toolbar button**: `🛡 HidHide` opens native HidHide configuration client. Grayed out when not installed.
- **Startup check**: `PrerequisitesWarningDialog` shown if vJoy or HidHide is absent/incompatible.
- **No in-app config page**: Device hiding configuration fully delegated to the native HidHide client. `IHidHideManager` exposes only `InitializeAsync` + `Dispose` — no Apply/Revert/Status/StatusChanged.

## Remaining Optional Features

- In-app GitHub Releases version checker (compare semver, show download link, no auto-install)
- Response curve editor (axes)
- Condition-based action pipeline
- UI for button mapping configuration

(Closed 2026-06-05: "`ProfileLibrary.ScanCore` async + parallel JSON read" — the per-file
trigger read it targeted was removed by the v12.1 global auto-load rework.)
