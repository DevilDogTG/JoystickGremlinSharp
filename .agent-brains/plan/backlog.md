# Project Roadmap

## Completed

- [x] **feature/autoload-rework** — Auto-load process→profile rework *(released v10.6.0; plan archived 2026-05-29)*
- [x] **feature/wix-installer** — Replace Velopack with WiX v6 MSI installer *(PR #62, ready to merge)*

  - [x] Remove Velopack NuGet + `VelopackApp.Build()` from app
  - [x] WiX v6 `.wixproj` + `Package.wxs` with wizard UI, path selection, optional desktop shortcut
  - [x] Update `build-installer.ps1` + `publish.yml` CI
  - [x] Code review findings resolved (2 warnings, 2 style)
  - [x] Re-review verdict: Approved

- [x] **feature/autoload-into-profile** — v11.0 breaking change: embed auto-load triggers inside each profile; consolidate settings root at `%APPDATA%\JoystickGremlinSharp\`; hard break, no migration. *(PR #66 — merging via workflow-end 2026-05-29; also bundles the dill.dll startup-crash fix)*

  - [x] `Profile.AutoLoadTriggers` + `ProcessTrigger` (drops `ProfilePath` back-pointer)
  - [x] `ProcessMatchType` moved to `Core.Profile` namespace
  - [x] `ProcessProfileResolver` signature returns `ProcessTriggerMatch(profile, trigger)`
  - [x] `ProfileEntry` extended with `AutoLoadTriggers` snapshot; `ProfileLibrary.ScanCore` reads them via DTO
  - [x] `ProcessMonitorService` sources triggers from library
  - [x] UI restructured around per-profile groups (`ProfileTriggersGroupViewModel` + `ProcessTriggerViewModel`)
  - [x] Tests: 332/332 passing (deleted `ProcessProfileMappingCompatTests`; new round-trip + trigger-surfacing coverage)
  - [x] BREAKING-CHANGES.md added; version bumped 10.6.2 → 11.0.0
  - [x] Code review pass 1 + re-review (stamp clean)

- [x] **fix/startup-crash-dill-cwd** — Non-admin users crashed on installed builds because bundled `dill.dll` writes `dill_debug.log` to CWD. Fix: CWD relocation in `DillDeviceManager.InitializeNative` + global exception handlers in `Program.Main` + try/catch on `MainWindow.Opened` async lambda. *(Cherry-picked into PR #66 on 2026-05-29; merging together)*

- [x] **chore/cleanup-hidhide-and-ci** — Post-v11 cleanup pass *(2026-05-29)*
  - [x] Deleted `.github/workflows/dotnet-ci.yml` (build+test moved to PR-review trust)
  - [x] Stripped HidHide Apply/Revert event-pipeline dead code from `HidHideManager`
  - [x] Removed dead `AppSettings` fields (`EnableHidHide`, `AutoHideOnPipelineRun`, `HiddenDeviceInstanceIds`, `HiddenDevices`)
  - [x] Deleted `HidHideStatus` enum and `HiddenDeviceEntry` type files
  - [x] Updated AGENTS.md HidHide section to match shipping reality
  - [x] Trimmed `HidHideManagerTests` (332 → 314 tests, all passing, 0 warnings)

## Backlog (Optional Features)

- [ ] **In-app GitHub Releases version checker** — check latest release, compare semver, show download link (no auto-install)
- [ ] Response curve editor (axes)
- [ ] Condition-based action pipeline
- [ ] UI for button mapping configuration
- [ ] **`ProfileLibrary.ScanCore` async + parallel JSON read** *(deferred from PR #66 code review)* — currently per-file synchronous read of triggers during scan; becomes a perceptible hitch beyond ~50 profiles. Refactor `ReadTriggers` → `ReadTriggersAsync` and parallelize via `Task.WhenAll` when profile count growth makes it visible.
