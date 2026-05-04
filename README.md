# Joystick Gremlin Sharp

A C# / .NET 10 rewrite of [JoystickGremlin](https://github.com/WhiteMagic/JoystickGremlin) — a Windows application for configuring joystick and gamepad devices. Map physical inputs to virtual vJoy axes, buttons, and hats; build macros; apply keyboard mappings; and configure advanced action pipelines.

Built with **Avalonia UI** and **ReactiveUI** for a modern, maintainable MVVM architecture on .NET 10.

> **Status**: Active development — 236 tests passing. Core pipeline, bindings editor, input viewer, process-monitor auto-load, force feedback bridge, and multi-button virtual output mapping complete.

---

## Features

- **Device detection** — lists all physical joystick/gamepad devices via DILL (DirectInput)
- **Input viewer** — live axis, button, and hat readout per device (configurable refresh rate)
- **Profile system** — flat JSON profiles (no modes) saved per-file; folder-based library with category support via subfolders
- **Bindings editor** — three-panel editor: device → input slot → bound actions
- **Action types**:
  - Map to vJoy axis / button / hat
  - Map to keyboard (Hold, Toggle, Press-Only, Release-Only behaviours)
  - Macro (key sequence on press or release)
  - Buttons-to-Hat (four physical buttons → vJoy POV/hat with directional state tracking)
  - Buttons-to-Axes (four physical buttons → dual vJoy axes, e.g. WASD → analog stick)
  - Hat-to-Axes (physical hat → dual vJoy axes)
- **Force feedback bridge** — forwards FFB commands from a game (via vJoy) to a physical DirectInput racing wheel (e.g. MOZA)
- **Process monitor** — automatically loads a profile when a configured executable becomes the active window
- **Avalonia UI** — left-sidebar navigation: Controller Setup, Virtual Devices, Profile, Settings, and About pages
- **About page** — displays application version, GitHub repository link, and license information

---

## Prerequisites

| Dependency | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Runtime + build tools |
| [vJoy (BrunnerInnovation fork)](https://github.com/BrunnerInnovation/vJoy/releases) | 2.2.x+ | Virtual joystick driver (Windows) |
| Windows | 10+ | Device I/O via vJoy and DILL is Windows-only |

> **Note**: The original vJoy on SourceForge is no longer maintained. Use the [BrunnerInnovation fork](https://github.com/BrunnerInnovation/vJoy/releases).

---

## Build & Run

```powershell
# Clone
git clone https://github.com/DevilDogTG/JoystickGremlinSharp.git
cd JoystickGremlinSharp

# Build all projects
dotnet build

# Run tests (236 tests)
dotnet test

# Run the application
dotnet run --project src/JoystickGremlin.App
```

---

## Solution Structure

```
src/
  JoystickGremlin.Core/         # Domain logic — profile, events, actions, FFB bridge, process monitor
  JoystickGremlin.Interop/      # P/Invoke wrappers for vJoy + DILL (Windows only)
  JoystickGremlin.App/          # Avalonia MVVM application (views, view-models, DI bootstrap)
tests/
  JoystickGremlin.Core.Tests/   # xUnit tests for Core domain (236 tests)
```

---

## Architecture

| Layer | Project | Key Types |
|---|---|---|
| Domain | `Core` | `Profile`, `InputBinding`, `ProfileLibrary`, `EventPipeline`, `ActionRegistry`, `ForceFeedbackBridge` |
| Interop | `Interop` | `VJoyDeviceManager`, `DillDeviceManager`, `SendInputKeyboardSimulator`, `MozaFfbSink` |
| UI | `App` | `MainWindowViewModel`, `ControllerSetupPageViewModel`, `VirtualDevicesPageViewModel`, `AboutPageViewModel` |

**DI container**: `Microsoft.Extensions.DependencyInjection`  
**Reactive UI**: `ReactiveUI` (`ReactiveObject`, `ReactiveCommand`, `WhenAnyValue`)  
**Logging**: `Serilog` + `Microsoft.Extensions.Logging`

---

## Third-Party Licenses

License texts for all third-party dependencies are in the [`licenses/`](licenses/) directory.

| Dependency | License |
|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | MIT |
| [ReactiveUI](https://github.com/reactiveui/ReactiveUI) | MIT |
| [Serilog](https://github.com/serilog/serilog) | Apache 2.0 |
| [Microsoft.Extensions.*](https://github.com/dotnet/runtime) | MIT |
| [xunit](https://github.com/xunit/xunit) | Apache 2.0 |
| [FluentAssertions](https://github.com/fluentassertions/fluentassertions) | Xceed Community License |
| [Moq](https://github.com/devlooped/moq) | BSD-3-Clause |
| [coverlet](https://github.com/coverlet-coverage/coverlet) | MIT |
| [Bootstrap Icons](https://github.com/twbs/icons) | MIT |
| [vJoy (BrunnerInnovation)](https://github.com/BrunnerInnovation/vJoy) | MIT |
| [Velopack](https://github.com/velopack/velopack) | MIT |

---

## Credits

JoystickGremlinSharp is a C# rewrite derived from **JoystickGremlin** by
[WhiteMagic](https://github.com/WhiteMagic/JoystickGremlin). The original Python
implementation and the DILL input library are the work of the original author and
contributors. This project would not exist without their foundational work.

See [`licenses/joystick-gremlin.txt`](licenses/joystick-gremlin.txt) for full attribution details.

---

## License

GPL-3.0-only — see [LICENSE](LICENSE).
