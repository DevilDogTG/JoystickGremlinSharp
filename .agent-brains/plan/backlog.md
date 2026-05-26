# Project Roadmap

## Completed

- [x] **feature/hidhide-startup-ux** — HidHide startup UX + admin privilege optimization *(merged → main via PR #54, 2026-05-26)*
  - [x] Combined prerequisite warning dialog (vJoy + HidHide)
  - [x] Auto-whitelist own exe at startup (`EnsureWhitelistedAsync`)
  - [x] Toolbar `🛡 HidHide` button → opens native HidHide client
  - [x] Check-before-write to minimize admin calls
  - [x] `asInvoker` manifest + on-demand UAC for CLI writes
  - [x] Remove HidHide integration page; delegate to native client

- [x] **bugfix/virtual-devices-refresh-rate** — Virtual Devices page visual lag fix *(PR #55 — ready to merge)*
  - [x] Replaced hardcoded 200ms interval with ISettingsService-driven dynamic throttle
  - [x] Used base-tick + TickCount64 pattern (matches ControllerSetupPageViewModel)
  - [x] Code review: 0 critical, 1 warning (resolved), verdict ✅ Ready to merge

## Backlog (Optional Features)

- [ ] Response curve editor (axes)
- [ ] Condition-based action pipeline
- [ ] UI for button mapping configuration
