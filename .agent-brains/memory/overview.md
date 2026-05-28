# Project Overview

## JoystickGremlinSharp — C# rewrite of JoystickGremlin

## Key Decisions

- **ADR-0001** (2026-05-25): `asInvoker` manifest + on-demand UAC for HidHide CLI subprocesses.
  See `.agent-brains/decisions/ADR-0001-on-demand-uac-for-hidhide-cli.md`.
- **ADR-0002** (2026-05-28): Replace Velopack with WiX v6 MSI installer (`perMachine`, WixUI_Mondo).
  Rationale: Velopack installs per-user (HKCU), causing app to be invisible in Windows Installed Apps.
  WiX 6.0.2 chosen over WiX 7.0.0 (7.0.0 introduced commercial maintenance fee).


**Branch model**: main-first, tag-based releases. Feature branches → rebase-merge PR → main.

**Test baseline**: 328 tests, 0 warnings (as of `feature/wix-installer` merged, 2026-05-28).

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

## HidHide Integration

HidHide is an optional device-hiding driver by Nefarius. Our integration:
- **Auto-whitelist**: App whitelists its own exe at startup so it can see devices HidHide may be hiding.
- **On-demand UAC**: App runs as `asInvoker` (no forced admin). If CLI write needs elevation, Windows UAC prompt appears just for that subprocess. User can decline safely.
- **Toolbar button**: `🛡 HidHide` opens native HidHide configuration client. Grayed out when not installed.
- **Startup check**: `PrerequisitesWarningDialog` shown if vJoy or HidHide is absent/incompatible.
- **No in-app config page**: Device hiding configuration fully delegated to the native HidHide client.

## Remaining Optional Features

- In-app GitHub Releases version checker (compare semver, show download link, no auto-install)
- Response curve editor (axes)
- Condition-based action pipeline
- UI for button mapping configuration
