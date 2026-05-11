# EmuWheel v2 — Implementation Plan

## Problem statement

Three previous attempts (`feature/emuwheel-backend`, the Brunner-vJoy spoof, the
`CM_Reenumerate_DevNode` attempt) all failed because **stock `vjoy.sys` hardcodes
`VID_1234 / PID_BEAD` as compile-time constants** (`VENDOR_N_ID` / `PRODUCT_N_ID`
in `inc/public.h`). Registry writes to `Parameters\DeviceXX\VendorID` are
silently ignored by the driver, so VID/PID-whitelist games (Forza Horizon 4/5,
The Crew 2, NFS) never see a wheel. The HID report descriptor is also baked
into the driver image, so games doing usage-page detection don't see a wheel
either.

We now want to deliver a real **virtual wheel feature** that:

1. Makes a virtual device appear as a recognised racing wheel (Logitech G29 /
   G920, Thrustmaster T300RS / TMX) — both VID/PID **and** HID descriptor.
2. Bridges Force Feedback from games to the user's physical wheel (FFB is a
   required goal — confirmed with user).
3. Manages **HidHide** as a first-class dependency from inside our app
   (auto-install prompt, hide/unhide physical/virtual devices, edit per-app
   whitelist) — confirmed with user.
4. Auto-loads and activates the correct profile when a target game executable
   starts — confirmed with user (per-game mapping + auto-activate).
5. Is **not strictly tied to vJoy** — backend is an abstraction so we can swap
   in a different virtual-HID source (custom vJoy fork, future ViGEm wheel
   target, etc.) without rewriting the action layer.

## Confirmed constraints (from clarifying Q&A)

| # | Constraint | User answer |
|---|---|---|
| C1 | HidHide integration | Required dependency, fully managed by our app |
| C2 | Driver signing posture | Test-signing mode acceptable (`bcdedit /set testsigning on`) for shipping an unsigned custom driver |
| C3 | FFB requirement | Essential — must work in racing games |
| C4 | Multi-backend | OK to introduce a second virtual-device backend alongside stock vJoy |
| C5 | Profile auto-load | Per-game profile mapping **and** auto-activate when game runs |
| C6 | Target games | Not yet decided — plan must support both VID/PID-whitelist and HID-usage detection games |

## Backend options researched (must be presented to user before coding)

| ID | Approach | Wheel detection | FFB | Signed | Effort | Risk |
|---|---|---|---|---|---|---|
| **B1** | Stock vJoy + HidHide only (cosmetic) | ❌ generic joystick | ✅ vJoy FFB exists | ✅ | low | useless for whitelist games |
| **B2** | Stock vJoy + binary-patch `Parameters\DeviceNN\HidReportDescriptor` to declare wheel usages (`Usage 0x04` + `Steering / Brake / Accelerator / Clutch`) | ⚠ HID-usage games only (Assetto, iRacing, AMS2, Project CARS) | ✅ vJoy FFB | ✅ stock driver, no resign | medium | will not satisfy Forza/Crew2; descriptor format is reverse-engineered |
| **B3** ★ | **Fork BrunnerInnovation/vJoy**, change `VENDOR_N_ID` / `PRODUCT_N_ID` (per-device override via registry) + replace HID descriptor with G29-shaped one + bundle as second backend `JGSWheel.sys`. Stock vJoy stays as the generic-controller backend | ✅ both VID/PID and HID usage | ✅ vJoy FFB pipeline reusable | ❌ test-sign for now (C2); EV-sign later | high | maintenance burden, kernel driver |
| B4 | Brand-new KMDF/UMDF HID minidriver from scratch | ✅ full control | manual | ❌ | very high | months of work |
| B5 | ViGEmBus fork with custom HID descriptor | ✅ already signed cert lost on fork | unknown FFB on non-Xbox/DS4 targets | ❌ once forked | high | loses Nefarius signature |
| B6 | Hardware: GIMX / FreeJoy adapter | ✅ on PC | ✅ | hardware | n/a | requires hardware purchase |

★ **Recommendation: B3 (fork vJoy as second backend) + B1 fallback** — gives FFB
out of the box (vJoy FFB stack is mature), satisfies both whitelist and
usage-detection games, and aligns with C2/C4. Stock vJoy stays installed for
existing generic-joystick profiles.

## Architecture

### Backend abstraction (Core)

New interface in `JoystickGremlin.Core`:

```csharp
public interface IVirtualDeviceBackend
{
    string Id { get; }                  // "vjoy", "jgs-wheel", future...
    string DisplayName { get; }
    BackendKind Kind { get; }           // GenericController | RacingWheel
    BackendStatus Status { get; }       // NotInstalled | Installed | NeedsTestSigning | Ready | Error
    Task<IReadOnlyList<IVirtualDevice>> EnumerateAsync(CancellationToken ct);
    Task<IVirtualDevice> AcquireAsync(uint id, CancellationToken ct);
    BackendCapabilities Capabilities { get; }   // FFB, axes, buttons, hats, identity-spoof
}
```

Existing `VJoyBackend` becomes the first implementation. `JgsWheelBackend`
(custom vJoy fork) becomes the second. `IVirtualDevice` stays the runtime
contract used by action functors, so existing `vjoy-axis` / `vjoy-button` /
`vjoy-hat` / `buttons-to-*` actions work unchanged when the backend is swapped.

### New backend: `JgsWheelBackend` (the wheel fork)

- **Driver source**: hard-fork of `BrunnerInnovation/vJoy` v2.2.x into
  `external/jgs-wheel/` as a git submodule (or vendored tree).
- **Code changes in the fork**:
  1. `inc/public.h` — change `VENDOR_N_ID` / `PRODUCT_N_ID` defaults to
     `0x046D / 0xC24F` (G29).
  2. `driver/sys/hid.c::vJoyGetDeviceAttributes` — read VID/PID per-device
     from `Parameters\DeviceNN\VendorID` / `ProductID`, fall back to defaults
     (so a single driver supports G29 / G920 / T300RS / TMX selection at
     runtime without rebuild).
  3. Replace static HID report descriptor with a **wheel descriptor**
     declaring: Usage Page 0x01, Usage 0x04 (Joystick) + Generic Desktop
     Steering / Accelerator / Brake / Clutch (Usage codes 0xC8 / 0xC4 / 0xC5 /
     0xC6 from HID Usage Tables Game Controls page) + 32 buttons + 1 hat.
  4. Service / class GUID renamed so it co-installs with stock vJoy without
     conflict (`jgswheel.sys`, `\\.\jgswheel`).
- **Build**: WDK 10 + Visual Studio Build Tools — script in
  `installer/wheel-driver/build.ps1`. Output: `jgswheel.sys` + `jgswheel.inf`
  + matching `JgsWheelInterface.dll` (port of `vJoyInterface`).
- **Signing**: test-signing for now (C2). Installer runs
  `bcdedit /set testsigning on` only after explicit user opt-in dialog
  explaining the implications (lower system security, watermark on desktop).
  EV-sign upgrade path documented in `docs/jgs-wheel-driver.md`.

### Interop layer

New project `JoystickGremlin.Interop.JgsWheel/`:
- `JgsWheelNative.cs` — P/Invoke to `JgsWheelInterface.dll`.
- `JgsWheelDevice.cs` — `IVirtualDevice` impl (mirrors `VJoyDevice`).
- `JgsWheelBackend.cs` — `IVirtualDeviceBackend` impl.
- `JgsWheelInstaller.cs` — wraps `pnputil /add-driver jgswheel.inf /install`,
  detects test-signing state, prompts to enable, schedules reboot if needed.
- `WheelIdentityRegistry.cs` — writes per-device VID/PID +
  `WheelModel` (G29 / G920 / T300RS / TMX preset) to
  `HKLM\SYSTEM\CurrentControlSet\Services\jgswheel\Parameters\DeviceNN`.
  Calls re-enumeration on the **bus PDO** (parent of HID FDO) — that's the
  enumeration node that actually responds to `CM_Reenumerate_DevNode`, fixing
  the previous attempt's no-op call on the leaf HID device.

### FFB plumbing

- `IForceFeedbackBridge` already exists in Core — extend it to declare the
  **active output backend** so a single wheel target receives FFB from the
  game.
- Source = `JgsWheelDevice` (vJoy FFB packet stream — same format as vJoy).
- Sink = existing DirectInput wheel sink (`Interop` already implements it for
  MOZA R9 etc.).
- No new domain types needed — the bridge state machine already handles
  source / sink hot-plug.

### HidHide integration (`JoystickGremlin.Interop.HidHide/`)

- `HidHideClient.cs` — wraps `HidHideCLI.exe` (subprocess + parses output)
  **and** the COM `HidHideClient.HidHideClient` ProgID for richer ops.
- Operations exposed:
  - `IsInstalled()`, `Install()` (downloads MSI from
    `https://github.com/nefarius/HidHide/releases`, runs silent install).
  - `EnableCloak() / DisableCloak()` — global gate.
  - `HideDeviceAsync(string instanceId)` / `UnhideDeviceAsync(...)`.
  - `GetWhitelist() / SetWhitelist(IEnumerable<string> exePaths)`.
- New Core service `IDeviceCloakService` — orchestrates HidHide based on the
  active profile (which physical devices to hide) and per-game whitelist
  (which exes are allowed to see them).
- New settings page section: `HidHide` — install status, master enable,
  device-by-device hide list, per-app whitelist.

### Profile model extensions (Core)

Add to `Profile`:

```csharp
public string? PreferredBackendId { get; set; }       // "vjoy" or "jgs-wheel"
public WheelIdentity? WheelIdentity { get; set; }     // model + custom VID/PID
public ProfileAutomation Automation { get; set; }     // game-launch trigger
public IList<string> HideDeviceInstanceIds { get; set; } = [];
public IList<string> HidHideWhitelistApps { get; set; } = [];
```

`WheelIdentity = (WheelModel Model, ushort? VendorIdOverride, ushort? ProductIdOverride)`
with presets: `G29`, `G920`, `T300RS`, `TMX`, `Custom`.

`ProfileAutomation = (string? ExecutableName, string? WindowTitleRegex,
bool ActivateOnLaunch, bool DeactivateOnExit)`.

`LegacyProfileMigrator` defaults all new fields to null/empty for old files.

### Auto-activation engine (Core + App)

- New Core service `IGameLauncherWatcher`:
  - Polls `Process.GetProcesses()` every 2s (low CPU; configurable in
    Settings) and matches each profile's `Automation.ExecutableName`.
  - Foreground-window fallback via `GetForegroundWindow` + `GetWindowText`
    when `WindowTitleRegex` is set (covers UWP/MS Store games).
- On match → `IProfileState.LoadAsync(matchedPath)` →
  `EventPipeline.Start()` → `IDeviceCloakService.ActivateForProfileAsync()`.
- On exit → reverse the chain; restore previous profile (or stop).
- App tray icon shows "🟢 Active: <profile>" with a balloon notification on
  switch (re-uses `TrayMenuService` from PR #45-ish).

### UI additions (`JoystickGremlin.App`)

| Page | Change |
|---|---|
| `VirtualDevicesPageView` | Tabbed: **vJoy** (existing) + **JGS Wheel** (new) — driver install state, identity preset picker, per-device VID/PID override, test-signing toggle |
| `SettingsPageView` | New **HidHide** card — install status, master enable, device-hide checklist, whitelist editor; new **Auto-activation** card — global enable + poll interval |
| `ProfilePageView` | Per-profile: backend picker, wheel identity, automation rule, hidden devices, whitelist apps (all driven by new `ProfileAutomationViewModel`) |
| `ControllerSetupPageView` | Backend filter dropdown above virtual device list |
| New `WheelEmulationDialog` | First-run wizard: explains test-signing, downloads HidHide, installs `jgswheel.sys`, picks default wheel identity |

### Installer changes

- `installer/build-installer.ps1` bundles `jgswheel.sys` + `jgswheel.inf` +
  `JgsWheelInterface.dll` into a `WheelDriver/` folder inside the Velopack
  package.
- Setup runs `pnputil /add-driver` only if user opts in on the wizard's
  EmuWheel page (default off — keeps the app safe for non-wheel users).
- HidHide is installed on demand from the app, **not** by the installer
  (avoids bundling Nefarius MSI redistribution).

## Open questions still needing user input

- **Q-A** Pick the supported game tier (Constraint C6 was skipped):
  - Tier-A only (Assetto, iRacing, AMS2, Project CARS) → can ship B2 first
    and B3 later.
  - Tier-B included (Forza, Crew 2, NFS) → must ship B3 from day one.
- **Q-B** Where do we keep the driver fork? Submodule under
  `external/jgs-wheel/` or vendored copy under `installer/wheel-driver/src/`?
- **Q-C** EV cert acquisition timeline — do we go test-signing-only forever,
  or pursue a one-time EV-sign before public release?
- **Q-D** Auto-activation polling — process-list scan (cheap, 2 s default) or
  ETW kernel-process-create subscription (no polling, needs admin)?

## Workflow rules (added per user feedback)

- **Branch**: all work happens on a new feature branch
  `feature/emuwheel-v2-wheel-backend` cut from latest `main` after
  `git fetch origin && git checkout main && git pull origin main`. No commits
  on `main`.
- **Atomic commits**: each commit must be a single coherent change (e.g.
  "Add IVirtualDeviceBackend interface + DI registration",
  "Refactor VJoyDevice behind backend abstraction",
  "Vendor Brunner vJoy fork as external/jgs-wheel"). Squash-merge is disabled
  in this repo, so the linear rebase-merge history will preserve every step.
  Run `dotnet build --configuration Release -warnaserror && dotnet test`
  before each commit; never commit a red build.
- **Mirror plan in the repo**: this session plan is also published to
  `docs/emuwheel-v2-plan.md` (created in the first commit of phase 1) so the
  user can read it side-by-side with the code. The session-folder copy stays
  the working source of truth and is updated as scope evolves; meaningful
  scope changes are mirrored back to the in-repo doc as their own commit
  (e.g. "docs: update EmuWheel v2 plan — Phase 5 scope change").
- **PR cadence**: open one PR per completed phase against `main` (rebase on
  main first, run build + tests, push). Phase 8 (docs & release) is its own
  PR that triggers the release workflow.

## Phased delivery

> Phases below assume B3 is approved. If user picks Tier-A only, phase 1 swaps
> B3 work for B2 (descriptor binary-patch on stock vJoy).

### Phase 1 — Backend abstraction + JgsWheel skeleton ✅ DONE
- `IVirtualDeviceBackend` + `BackendRegistry` shipped in Core.
- `Profile.PreferredBackendId` (nullable, omitted-when-null for legacy
  compatibility) shipped with round-trip tests.
- 0 behavioural changes to existing vJoy stack.

### Phase 2 — Custom wheel driver fork ✅ SCAFFOLDED
- `installer/wheel-driver/` skeleton committed: `README.md`, `build.ps1`
  (clones BrunnerInnovation/vJoy@v2.2.2, applies patches, builds with WDK,
  optionally test-signs), `patches/README.md` listing the four planned
  patches, `.gitignore`.
- Driver binaries (`jgswheel.sys`, `JgsWheelInterface.dll`) are produced by
  the developer running `build.ps1` on a WDK-equipped machine; not built in
  CI yet (no WDK on the runner).

### Phase 3 — Interop + Backend impl ✅ DONE (graceful stub)
- `JgsWheelPrerequisiteChecker` probes the registry for an installed driver.
- `JgsWheelDeviceManager` implements `IVirtualDeviceManager` and degrades
  safely to `BackendStatus.NotInstalled` when the driver / interface DLL is
  absent. `AcquireDevice` throws `VJoyException` with a build-instruction
  hint pointing at `installer/wheel-driver/README.md`.
- `JgsWheelBackend` (Id `jgs-wheel`, Kind `RacingWheel`) registers in DI
  behind `AppSettings.EnableJgsWheelBackend` (default `false`).
- 16 new tests cover prerequisite probe + backend wiring.

### Phase 4 — FFB bridging ✅ DONE (graceful stub)
- `JgsWheelFfbSource` implements `IForceFeedbackSource` with the same packet
  contract as `VJoyFfbSource`. Surfaces no events until the driver is
  installed; bridge state machine handles source / sink hot-plug as before.
- 5 new tests cover lifecycle.

### Phase 5 — HidHide integration ✅ DONE
- `IHidHideService` Core abstraction + `HidHideStatus` /
  `HidHideDeviceEntry` / `HidHideAppWhitelistEntry` records.
- `NullHidHideService` no-op default registered by Core via `TryAddSingleton`.
- `HidHideCliService` in Interop shells out to `HidHideCLI.exe` (auto-detect
  via standard install paths) and parses `--dev-list` / `--app-list`. Replaces
  the Core null impl in DI.
- `AppSettings.EnableHidHide` and `AppSettings.HidHideCliPath` added.
- 8 new tests (parser + null-impl).

### Phase 6 — Profile automation ✅ DONE (already shipped)
- `ProcessProfileMapping`, `IProcessMonitor`, `WindowsProcessMonitor`,
  `ProcessProfileResolver`, `ProcessMonitorService`, `AppSettings.EnableAutoLoading`
  and `AppSettings.ProcessMappings` were already implemented in a prior
  release. No new code required.

### Phase 7 — UI polish & first-run wizard 🔜 FOLLOW-UP
- Settings page: HidHide card (install status, master enable, CLI path
  picker, device list, whitelist editor).
- VirtualDevices page: JGS Wheel tab with install state + identity preset.
- Profile editor: backend picker bound to `Profile.PreferredBackendId`,
  per-profile hide list / whitelist.
- `installer/build-installer.ps1` bundles driver files; opt-in on setup.
- **Status**: settings model and Core abstractions are in place; visible UI
  bindings are deferred to a follow-up PR with interactive design review.

### Phase 8 — Docs & release ✅ DONE (docs)
- `docs/jgs-wheel-driver.md` — driver build, test-signing, EV-sign upgrade.
- `docs/hidhide-integration.md` — what we hide, what we expose.
- `docs/profile-automation.md` — per-game mapping examples.
- Release tag is reserved for the follow-up PR that lands Phase 7.

## Risks & mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Test-signing watermark + lower security disliked by users | adoption | clearly opt-in; document EV-sign upgrade; provide "uninstall driver" path |
| vJoy fork drifts from upstream Brunner fixes | maintenance | track upstream as a remote; quarterly merge windows |
| HidHide MSI URL changes | install flow breaks | resolve URL via Nefarius `latest` GitHub Release API at install time |
| Game's anti-cheat flags virtual wheel | bans | document risk in wizard; recommend offline-only games initially |
| Process polling misses fast-launching games | UX | make poll interval configurable; offer ETW path as Q-D follow-up |
| FFB latency over the bridge | feel | reuse existing `ForceFeedbackBridge` with measured latency tests |

## Success criteria

- Forza Horizon 4 detects the JGS Wheel as a Logitech G29 and accepts steering /
  pedals / FFB. (verifies B3)
- Assetto Corsa Competizione binds steering / brake / throttle / clutch via
  HID-usage detection. (verifies descriptor)
- HidHide hides the user's physical MOZA wheel from Forza but keeps it visible
  to our app, all driven by the active profile.
- Launching `ForzaHorizon4.exe` auto-activates the matching profile within
  3 seconds and the tray icon updates.
- Closing the game restores the previous profile / stops the pipeline.
- All 236 existing tests still pass; new feature ships with ≥ 30 new tests
  (backend abstraction, profile migration, HidHide client, watcher).
