# Joystick Gremlin Sharp

A C# / .NET 10 port of [JoystickGremlin](https://github.com/WhiteMagic/JoystickGremlin), a Windows joystick configuration tool that lets you remap physical joystick inputs to virtual vJoy devices, apply response curves, build macros, and use a flexible mode system.

Built with **Avalonia UI** for cross-platform UI portability and **.NET 10** for modern performance.

> **Status**: Active development — Phase 4 (UI) in progress.

---

## Features

- Works with any joystick-like device recognised by Windows (DILL)
- Maps physical inputs → virtual vJoy axes, buttons, and hats
- Flexible mode system with inheritance and runtime switching
- JSON-based profiles (saved to `%APPDATA%\JoystickGremlin\`)
- Avalonia UI with left-sidebar navigation (Devices, Profile, Settings)

---

## Prerequisites

| Dependency | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Runtime + build tools |
| [vJoy](https://sourceforge.net/projects/vjoystick/) | 2.1.9+ | Virtual joystick driver |
| Windows | 10+ | Device I/O is Windows-only |

---

## Build & Run

```powershell
# Clone
git clone https://github.com/DevilDogTG/JoystickGremlinSharp.git
cd JoystickGremlinSharp

# Build all projects
dotnet build

# Run tests
dotnet test tests/JoystickGremlin.Core.Tests

# Run the application
dotnet run --project src/JoystickGremlin.App
```

---

## Solution Structure

```
src/
  JoystickGremlin.Core/         # Domain logic — profile, modes, events, actions
  JoystickGremlin.Interop/      # P/Invoke wrappers for vJoy + DILL (Windows only)
  JoystickGremlin.App/          # Avalonia MVVM application
tests/
  JoystickGremlin.Core.Tests/   # xUnit tests for Core domain
```

---

## Architecture

| Layer | Project | Key Types |
|---|---|---|
| Domain | `Core` | `Profile`, `Mode`, `InputBinding`, `ModeManager`, `EventPipeline` |
| Interop | `Interop` | `VJoyDeviceManager`, `DillDeviceManager`, P/Invoke wrappers |
| UI | `App` | `MainWindowViewModel`, `DevicesPageViewModel`, Avalonia AXAML views |

**DI container**: `Microsoft.Extensions.DependencyInjection`  
**Reactive UI**: `ReactiveUI` (`ReactiveObject`, `ReactiveCommand`, `WhenAnyValue`)  
**Logging**: `Serilog` + `Microsoft.Extensions.Logging`

---

## Contributing

See [AGENTS.md](AGENTS.md) for development conventions, build commands, and architecture guidance.

---

## License

GPL-3.0-only — see [LICENSE](LICENSE).
