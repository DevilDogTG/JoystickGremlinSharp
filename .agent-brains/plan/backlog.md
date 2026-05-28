# Project Roadmap

## Completed

- [x] **feature/autoload-rework** — Auto-load process→profile rework *(released v10.6.0)*

  - [x] Combined prerequisite warning dialog (vJoy + HidHide)
  - [x] Auto-whitelist own exe at startup (`EnsureWhitelistedAsync`)
  - [x] Toolbar `🛡 HidHide` button → opens native HidHide client
  - [x] Check-before-write to minimize admin calls
  - [x] `asInvoker` manifest + on-demand UAC for CLI writes
  - [x] Remove HidHide integration page; delegate to native client
  - [x] PR #53 created, reviewed, all findings resolved — **ready to merge**

## In Progress

- [ ] **feature/wix-installer** — Replace Velopack with WiX v6 MSI installer
  - [x] Remove Velopack NuGet + `VelopackApp.Build()` from app
  - [x] WiX v6 `.wixproj` + `Package.wxs` with wizard UI, path selection, optional desktop shortcut
  - [x] Update `build-installer.ps1` + `publish.yml` CI
  - [ ] Build verified, HKLM registration confirmed

## Backlog (Optional Features)

- [ ] **In-app GitHub Releases version checker** — check latest release, compare semver, show download link (no auto-install)
- [ ] Response curve editor (axes)
- [ ] Condition-based action pipeline
- [ ] UI for button mapping configuration
