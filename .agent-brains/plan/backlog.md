# Project Roadmap

## Completed

- [x] **feature/keyboard-behavior-descriptions** — plain-language descriptions for the
  map-to-keyboard behaviors in both binding editors (two-line dropdown items + caption)
  via shared `KeyBehaviorPicker` UserControl; behavior string canonicalized at form-load.
  Outcome of the 2026-06-05 "drop behaviors?" discussion — all four behaviors kept.
  Merged 2026-06-05, unreleased pending next version bump. *(PR #77, two review rounds,
  Approved. Plan archived: [archive/feature-keyboard-behavior-descriptions.md](archive/feature-keyboard-behavior-descriptions.md))*

- [x] **feature/version-checker** — In-app GitHub Releases version checker on the About page: `IUpdateChecker`/`GitHubUpdateChecker` in Core (tag parsing, version normalization, graceful failure), Updates section with status line + download button; removed orphaned `MainWindowViewModel.CheckForUpdatesCommand`; `.claude/settings.local.json` untracked + gitignored. Merged 2026-06-05, unreleased pending next version bump. *(PR #74, review round 1 Approved + cosmetic fixes. Tests 327 → 355. Plan archived: [archive/feature-version-checker.md](archive/feature-version-checker.md))*

- [x] **feature/global-autoload** — Auto-load triggers moved from profile-embedded to a global trigger store in `settings.json` (`AppSettings.AutoLoadTriggers`); idempotent auto-migration at startup + manual "Migrate now" banner; flat Auto-load page with per-row profile picker; released as v12.1.0 on 2026-06-05. *(PR #71, two review rounds, Approved. ADR-0003. Plan archived: [archive/feature-global-autoload.md](archive/feature-global-autoload.md))*

- [x] **feature/release-skill** — Workspace `release` skill (bump recommendation + confirm, curated `RELEASE-NOTES.md`, drives release/-PR → tag.yml → publish.yml chain); publish.yml switched to `body_path`. Verified live by the v12.1.0 release. *(PR #72 + release PR #73, 2026-06-05. Plan archived: [archive/release-skill.md](archive/release-skill.md))*

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

- [x] **chore/cleanup-hidhide-and-ci** — Post-v11 cleanup pass — PR #69, awaiting merge — *plan archived 2026-05-29*
  - [x] Deleted `.github/workflows/dotnet-ci.yml` (build+test moved to PR-review trust)
  - [x] Stripped HidHide Apply/Revert event-pipeline dead code from `HidHideManager`
  - [x] Removed dead `AppSettings` fields (`EnableHidHide`, `AutoHideOnPipelineRun`, `HiddenDeviceInstanceIds`, `HiddenDevices`)
  - [x] Deleted `HidHideStatus` enum and `HiddenDeviceEntry` type files
  - [x] Updated AGENTS.md HidHide section to match shipping reality
  - [x] Primary-constructor refactor of `HidHideManager`; dropped unused `CancellationToken` param
  - [x] Trimmed `HidHideManagerTests` and added `Dispose_CalledTwice` (332 → 315 tests, all passing, 0 warnings)
  - [x] Two code-review rounds via `global:code-review` — final verdict: Approved

## Backlog (Optional Features)

- [ ] **Winget distribution** — publish to microsoft/winget-pkgs (one-time manifest bootstrap) + auto-update PR step in publish.yml via `wingetcreate`. Feasibility confirmed 2026-06-05 — MSI/signing/URL setup already winget-ready. *(Plan: [feature-winget-distribution.md](feature-winget-distribution.md))*
- [ ] Undefined-numeric `behavior` guard — `Enum.TryParse` accepts `"99"` → undefined enum value (runtime no-op, blank picker). Add `Enum.IsDefined` in **both** `MapToKeyboardActionDescriptor.CreateFunctor` and the VM load path together, or not at all (one-sided fix makes UI and runtime diverge). *Deferred from PR #77 round-2 review; pick up when the Core parse is next touched.*

## Dropped (2026-06-05 — no plan for now; revisit only on concrete demand)

- [x] ~~Response curve editor (axes)~~ — *dropped 2026-06-05.* Scoping notes preserved: pipeline does NOT chain values between functors (EventPipeline dispatches the same immutable InputEvent to each action independently), so a chainable "response-curve" action à la Python JG would require an IActionFunctor/pipeline rework; the cheap alternative was an embedded `curve` config sub-object applied inside `VJoyAxisFunctor`.
- [x] ~~Condition-based action pipeline~~ — *dropped 2026-06-05* (was deferred pending modes-vs-conditions ADR)
- [x] ~~UI for button mapping configuration~~ — *dropped 2026-06-05* (Bindings page likely already covers it)
- [x] ~~**`ProfileLibrary.ScanCore` async + parallel JSON read**~~ *(closed 2026-06-05 — the per-file trigger read during scan was removed entirely by feature/global-autoload; ScanCore now only lists files)*
