# Joystick Gremlin Sharp

A C# / .NET 10 rewrite of [JoystickGremlin](https://github.com/WhiteMagic/JoystickGremlin) — a Windows application for configuring joystick, HOTAS, and racing-wheel devices. Map physical inputs to virtual vJoy axes, buttons, and hats; build macros; apply keyboard mappings; auto-switch profiles per game; and bridge force feedback to your physical wheel.

Built with **Avalonia UI** and **ReactiveUI** for a modern, maintainable MVVM architecture on .NET 10.

> **Status**: v11.0.0 — 332 tests passing, 0 warnings. Distributed as a signed WiX MSI installer (per-machine). v11 is a breaking change: see [BREAKING-CHANGES.md](BREAKING-CHANGES.md).

---

## Features

### Device & Input
- **Device detection** — lists all physical joystick / HOTAS / wheel / gamepad devices via DILL (DirectInput).
- **Input viewer** — live axis, button, and hat readout per device with configurable refresh rate (default 100 Hz; toggleable per-session to reduce UI load while gaming).
- **Unified input list** — single sortable view of all inputs across all connected devices.

### Profiles
- **Flat JSON profiles** — one profile per file, no nested modes; folder-based library with category support via subfolders.
- **Per-profile storage** — `%APPDATA%\JoystickGremlinSharp\profiles\`, customisable via Settings.
- **Quick-switch toolbar** — change the active profile from a dropdown in the top toolbar at any time.
- **Per-profile auto-load triggers** (v11) — each profile owns its own list of process triggers; sharing or copying a profile carries its triggers with it.

### Action Types
- **Map to vJoy axis / button / hat** — drive virtual devices from any physical input.
- **Map to keyboard** — Hold, Toggle, Press-Only, and Release-Only behaviours via `SendInput`.
- **Macro** — key sequences fired on press or release.
- **Buttons-to-Hat** — four physical buttons → vJoy POV / hat with directional state tracking (e.g. WASD → hat).
- **Buttons-to-Axes** — four physical buttons → dual vJoy axes (e.g. WASD → analog stick).
- **Hat-to-Axes** — physical hat → dual vJoy axes.

### Auto-Load (v11 design)
- **Process monitor** — when a configured executable becomes the active (focused) window, its owning profile loads and (optionally) auto-starts the pipeline.
- **Match modes** — by process name (case-insensitive) or full executable path.
- **Per-trigger options** — `AutoStart` (start pipeline on activation), `RemainActiveOnFocusLoss` (keep running after focus change), `IsEnabled` (per-trigger kill-switch), priority ordering.
- **Global kill-switch** — `Enable Auto-loading` checkbox on the Auto-load page.

### Force Feedback Bridge
- **Game → physical wheel** — forwards FFB commands written by a game (via vJoy) to a physical DirectInput racing wheel (MOZA wheels auto-detected).
- **Configurable gain** — 0–200 % output scaling.
- **Wheel selection** — auto-detect first MOZA device, or pin a specific DirectInput instance GUID.

### HidHide Integration *(optional driver by Nefarius)*
- **Auto-whitelist** — Joystick Gremlin Sharp whitelists its own executable at startup so it can see devices HidHide may be hiding from games.
- **On-demand UAC** — the app runs as a normal user (`asInvoker`); only the HidHide CLI subprocess elevates if it needs to write, via a single UAC prompt the user can decline.
- **Toolbar shortcut** — `🛡 HidHide` button opens the native HidHide configuration client (grayed out when not installed).
- **Startup prerequisites check** — warns if vJoy or HidHide is missing or incompatible.

### Application Shell
- **Avalonia UI** — left-sidebar navigation: Controller Setup, Virtual Devices, Profile, Auto-load, Settings, About.
- **System tray support** — start minimised, close-to-tray, and start-with-Windows (optional).
- **Start / Stop toggle** — single toolbar button to arm or disarm the event pipeline.
- **Live-input toggle** — pause UI-thread polling without stopping the pipeline.
- **About page** — version, GitHub link, license, and third-party attribution.

---

## Feature Comparison — JoystickGremlinSharp vs Original JoystickGremlin (Python)

The original [JoystickGremlin](https://github.com/WhiteMagic/JoystickGremlin) by [WhiteMagic](https://github.com/WhiteMagic) (Python / PyQt) is the foundation this project derives from. JoystickGremlinSharp is a ground-up rewrite, not a port; some features were intentionally dropped in favour of a simpler, more focused design, while others have been added.

Legend: ✅ available · 🚧 backlog / planned · ❌ intentionally not implemented · 🆕 new in JoystickGremlinSharp

| Capability | JoystickGremlin (Python) | JoystickGremlin**Sharp** (C#) |
|---|---|---|
| **Platform & runtime** | Python 3 + PyQt5 | .NET 10 + Avalonia 12 |
| **Distribution** | Bundled `.exe` (PyInstaller) / source | 🆕 Signed WiX MSI installer (per-machine, in *Installed Apps*) |
| **Modes** (named, switchable, hierarchical) | ✅ Full mode tree per profile | ❌ Removed — one flat profile per file; switch profiles instead |
| **Profile activation** | Manual + executable-based auto-load (global mapping list) | ✅ Manual + 🆕 **per-profile auto-load triggers** (triggers live inside each profile JSON) |
| **vJoy output** (axis / button / hat) | ✅ | ✅ |
| **Map to keyboard** | ✅ | ✅ Hold / Toggle / Press-Only / Release-Only |
| **Map to mouse** | ✅ | ❌ Not implemented |
| **Macros** | ✅ Recorded key / button sequences with timing | ✅ Key-sequence macros on press / release |
| **Buttons → Hat** | ✅ (via Hat Buttons container) | ✅ Buttons-to-Hat action |
| **Buttons → Axes** *(WASD-style)* | ❌ Not built in | 🆕 ✅ Buttons-to-Axes action |
| **Hat → Axes** | ✅ (via custom configuration) | ✅ Hat-to-Axes action |
| **Response curves** (axis shaping, deadzones) | ✅ Curve editor with cubic / spline | 🚧 Planned (see roadmap) |
| **Merge / split axes** | ✅ Merge Axis action | ❌ Not implemented |
| **Action containers** (Tempo, Chain, Cycle, Switch Mode, Pause/Resume) | ✅ | ❌ Removed with modes (planned condition pipeline may cover some cases) |
| **Activation conditions** (per-action) | ✅ | 🚧 Backlog — condition-based action pipeline |
| **User plugins / scripts** | ✅ Python user scripts with full SDK | ❌ Not implemented (and not planned) |
| **Force feedback bridge** *(game → physical wheel)* | ❌ Not built in | 🆕 ✅ vJoy FFB → DirectInput wheel (MOZA auto-detect, gain control) |
| **HidHide integration** | ❌ Manual / external | 🆕 ✅ Auto-whitelist + native client launcher + on-demand UAC |
| **Device calibration UI** | ✅ | ❌ Use Windows joy.cpl / DirectInput properties |
| **Input repeater / virtual cycle** | ✅ | ❌ Not implemented |
| **Process monitor** (foreground-window watch) | ✅ Global mapping | ✅ 🆕 Owned by each profile |
| **System tray** | Partial | ✅ Close-to-tray, start-minimised, start-with-Windows |
| **In-app updates** | ❌ Manual download | ❌ Removed (was Velopack in v10.x). **Check for Updates** opens GitHub Releases; semver checker on roadmap |
| **Settings UI** | ✅ | ✅ Settings page (profiles folder, tray, FFB, live-input refresh) |
| **Test coverage** | Limited | 🆕 332 unit tests, 0 warnings on the baseline |
| **License** | MIT | GPL-3.0-only |

### Why drop modes?
The mode system in the Python original was its most powerful — and most complex — feature. Real-world experience showed many users built one mode per game and never used mode switching within a profile. JoystickGremlinSharp replaces that with **one profile per game** plus per-profile auto-load triggers, which keeps the mental model flat and makes profiles portable. If you need mode-like behaviour, create multiple profiles and switch via the toolbar dropdown or a process trigger.

### Migrating from the Python original
There is no file-level migration path: profile files are not format-compatible. Rebuild profiles in JoystickGremlinSharp using the Bindings editor. The action vocabulary covers the most common use cases (vJoy / keyboard / macros / hat / buttons-to-axes); response curves and merge-axis users may want to wait for the response-curve editor on the roadmap.

---

## Prerequisites

| Dependency | Version | Required? | Notes |
|---|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Build only | Runtime + build tools |
| [vJoy (BrunnerInnovation fork)](https://github.com/BrunnerInnovation/vJoy/releases) | 2.2.x+ | Required | Virtual joystick driver (Windows) |
| [HidHide](https://github.com/nefarius/HidHide/releases) | latest | Optional | Hide physical devices from games; integration is fully opt-in |
| Windows | 10 or 11 | Required | Device I/O via vJoy and DILL is Windows-only |

> **Note**: The original vJoy on SourceForge is no longer maintained. Use the [BrunnerInnovation fork](https://github.com/BrunnerInnovation/vJoy/releases). The startup prerequisites dialog will warn you if either driver is missing or incompatible.

---

## Install

Grab the latest signed MSI from the [Releases page](https://github.com/DevilDogTG/JoystickGremlinSharp/releases) and run it. The installer:

- Installs per-machine (visible in **Settings → Apps** and **Control Panel → Programs and Features**).
- Offers a wizard with custom install path and feature tree (desktop shortcut is optional).
- Performs in-place major upgrades (uninstall not required between versions).

User data — profiles, settings, logs — lives under the user profile, not the install directory:

| What | Location |
|---|---|
| Profiles | `%APPDATA%\JoystickGremlinSharp\profiles\` |
| Settings | `%APPDATA%\JoystickGremlinSharp\settings.json` |
| Logs | `%LOCALAPPDATA%\JoystickGremlinSharp\logs\` |

> v11 consolidated all user data under `JoystickGremlinSharp\`. The legacy `%APPDATA%\JoystickGremlin\settings.json` from v10.x is no longer read; see [BREAKING-CHANGES.md](BREAKING-CHANGES.md).

---

## Build & Run (from source)

```powershell
# Clone
git clone https://github.com/DevilDogTG/JoystickGremlinSharp.git
cd JoystickGremlinSharp

# Build all projects
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project src/JoystickGremlin.App

# Build the MSI installer (Release)
dotnet build installer/JoystickGremlinSharp.wixproj -c Release
```

---

## Solution Structure

```
src/
  JoystickGremlin.Core/         Domain logic — profile, bindings, actions, FFB bridge,
                                process monitor, HidHide manager, event pipeline
  JoystickGremlin.Interop/      P/Invoke wrappers (vJoy, DILL, MOZA FFB, HidHide CLI,
                                SendInput, ProcessMonitor)
  JoystickGremlin.App/          Avalonia MVVM application (views, view-models, DI bootstrap)
installer/
  JoystickGremlinSharp.wixproj  WiX v6 MSI project (per-machine, WixUI_Mondo)
  Package.wxs                   Component definitions, shortcuts, upgrade policy
tests/
  JoystickGremlin.Core.Tests/   xUnit tests for Core domain (332 tests)
```

---

## Architecture

| Layer | Project | Key Types |
|---|---|---|
| Domain | `Core` | `Profile`, `ProcessTrigger`, `InputBinding`, `ProfileLibrary`, `EventPipeline`, `ActionRegistry`, `ForceFeedbackBridge`, `ProcessProfileResolver`, `HidHideManager` |
| Interop | `Interop` | `VJoyDeviceManager`, `DillDeviceManager`, `SendInputKeyboardSimulator`, `MozaFfbSink`, `HidHideCliClient` |
| UI | `App` | `MainWindowViewModel`, `ControllerSetupPageViewModel`, `VirtualDevicesPageViewModel`, `ProfilePageViewModel`, `AutoLoadPageViewModel`, `SettingsPageViewModel`, `AboutPageViewModel` |
| Installer | `installer/` | WiX v6 SDK project producing the signed MSI |

**DI container**: `Microsoft.Extensions.DependencyInjection`
**Reactive UI**: `ReactiveUI` (`ReactiveObject`, `ReactiveCommand`, `WhenAnyValue`)
**Logging**: `Serilog` + `Microsoft.Extensions.Logging`

---

## Roadmap (Optional Features)

The following are tracked in [.agent-brains/plan/backlog.md](.agent-brains/plan/backlog.md):

- In-app GitHub Releases version checker (semver compare; no auto-install)
- Response curve editor (axes)
- Condition-based action pipeline
- UI for richer button-mapping configuration
- Async / parallel `ProfileLibrary.ScanCore` (only relevant beyond ~50 profiles)

---

## Third-Party Licenses

License texts for all third-party dependencies are in the [`licenses/`](licenses/) directory.

| Dependency | License |
|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | MIT |
| [ReactiveUI](https://github.com/reactiveui/ReactiveUI) | MIT |
| [Serilog](https://github.com/serilog/serilog) | Apache 2.0 |
| [Microsoft.Extensions.*](https://github.com/dotnet/runtime) | MIT |
| [WiX Toolset](https://github.com/wixtoolset/wix) | MS-RL |
| [xunit](https://github.com/xunit/xunit) | Apache 2.0 |
| [FluentAssertions](https://github.com/fluentassertions/fluentassertions) | Xceed Community License |
| [Moq](https://github.com/devlooped/moq) | BSD-3-Clause |
| [coverlet](https://github.com/coverlet-coverage/coverlet) | MIT |
| [Bootstrap Icons](https://github.com/twbs/icons) | MIT |
| [vJoy (BrunnerInnovation)](https://github.com/BrunnerInnovation/vJoy) | MIT |
| [HidHide](https://github.com/nefarius/HidHide) (optional) | GPL-3.0 |

---

## Credits

JoystickGremlinSharp is a C# rewrite derived from **[JoystickGremlin](https://github.com/WhiteMagic/JoystickGremlin)** by
[WhiteMagic](https://github.com/WhiteMagic). The original Python implementation and the DILL input library are the work
of the original author and contributors. This project would not exist without their foundational work.

See [licenses/joystick-gremlin.txt](licenses/joystick-gremlin.txt) for full attribution details.

---

## License

GPL-3.0-only — see [LICENSE](LICENSE).
