# Project Roadmap

## Completed

- [x] **feature/hidhide-startup-ux** — HidHide startup UX + admin privilege optimization *(PR #53 ready to merge)*
  - [x] Combined prerequisite warning dialog (vJoy + HidHide)
  - [x] Auto-whitelist own exe at startup (`EnsureWhitelistedAsync`)
  - [x] Toolbar `🛡 HidHide` button → opens native HidHide client
  - [x] Check-before-write to minimize admin calls
  - [x] `asInvoker` manifest + on-demand UAC for CLI writes
  - [x] Remove HidHide integration page; delegate to native client
  - [x] PR #53 created, reviewed, all findings resolved — **ready to merge**

## Backlog (Optional Features)

- [ ] Response curve editor (axes)
- [ ] Condition-based action pipeline
- [ ] UI for button mapping configuration
