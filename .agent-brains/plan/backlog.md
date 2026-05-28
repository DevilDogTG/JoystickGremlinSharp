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

## In Progress

- [ ] **feature/autoload-rework** — Auto-load process→profile rework *(see [autoload-rework.md](autoload-rework.md))*
  - [ ] Process picker (windowed apps + game flag) replacing hand-typed exe/regex
  - [ ] Profile dropdown from `IProfileLibrary` (app-created profiles only)
  - [ ] Dedicated Auto-load page; remove auto-load grid from Settings
  - [ ] Match by exe name / manual path; drop regex; keep advanced toggles

## Backlog (Optional Features)

- [ ] Response curve editor (axes)
- [ ] Condition-based action pipeline
- [ ] UI for button mapping configuration
