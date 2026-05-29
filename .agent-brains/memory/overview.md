# Project Overview

## JoystickGremlinSharp — C# rewrite of JoystickGremlin

## Key Decisions

- **ADR-0001** (2026-05-25): `asInvoker` manifest + on-demand UAC for HidHide CLI subprocesses.
  See `.agent-brains/decisions/ADR-0001-on-demand-uac-for-hidhide-cli.md`.
- **ADR-0002** (2026-05-28): Replace Velopack with WiX v6 MSI installer (`perMachine`, WixUI_Mondo).
  Rationale: Velopack installs per-user (HKCU), causing app to be invisible in Windows Installed Apps.
  WiX 6.0.2 chosen over WiX 7.0.0 (7.0.0 introduced commercial maintenance fee).


**Branch model**: main-first, tag-based releases. Feature branches → rebase-merge PR → main.

**Test baseline**: 315 tests, 0 warnings (as of PR #69 merge, 2026-05-29 — drop from 332 reflects 17 deleted tests covering the removed HidHide Apply/Revert pipeline + 1 added `Dispose_CalledTwice` test).

**Current version**: v12.0.1 (v12.0.0 was the cleanup release; v12.0.1 was tagged immediately after). Previous breaking change at v11.0 — see `BREAKING-CHANGES.md` and Auto-Load section below.

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

## Auto-Load Triggers (v11.0+)

- **Per-profile ownership**: process triggers live inside each profile's
  `AutoLoadTriggers` list, not in a global `settings.json` mapping. Deleting or
  sharing a profile carries its triggers with it.
- **Storage paths** (consolidated under `%APPDATA%\JoystickGremlinSharp\`):
  - Profiles: `%APPDATA%\JoystickGremlinSharp\profiles\`
  - Settings: `%APPDATA%\JoystickGremlinSharp\settings.json` (moved from legacy
    `JoystickGremlin\` folder in v11)
  - Logs: `%LOCALAPPDATA%\JoystickGremlinSharp\logs\`
- **Resolver**: `ProcessProfileResolver.Resolve(string, IEnumerable<ProfileEntry>)`
  returns `ProcessTriggerMatch(profile, trigger)`. Iteration order is the
  library scan order (alphabetical by file path within each category).
- **Global kill-switch**: `AppSettings.EnableAutoLoading` still gates the whole
  feature.
- **Breaking change**: v10.x mappings in `%APPDATA%\JoystickGremlin\settings.json`
  are NOT migrated. See `BREAKING-CHANGES.md`.

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
- `ProfileLibrary.ScanCore` async + parallel JSON read (deferred from PR #66 code review; becomes relevant beyond ~50 profiles)
