# AGENTS.md - JoystickGremlinSharp Developer Guide

This file provides guidance for AI agents working on the JoystickGremlinSharp codebase.

> **Migration note**: The original Python/PySide6/QML source (under `gremlin/`, `action_plugins/`,
> `vjoy/`, `dill/`) is retained as a **reference implementation** only. All new development
> targets the C# solution described in this file.
> Legacy folders `doc/`, `gfx/`, and `gh_page/` have been removed (Python Sphinx docs and
> GitHub Pages from the original project — not applicable to the C# rewrite).

> **Phase status**: Phases 4–9 complete and merged to `develop`. 131 tests passing, 0 build warnings.
> PR #2 (`features/fix-input-viewer-keyboard`) merged. PR #3 (`features/installer-systemtray`) merged.
> **main branch created**. PR #4 (`release/v10.0.1` → `main`) and PR #5 (merge-back → `develop`)
> are open and ready to merge to complete the v10.0.1 release.
> Release pipeline requires either `RELEASE_TOKEN` secret (fine-grained PAT: Contents+PRs write)
> OR repo setting: Settings → Actions → General → "Allow GitHub Actions to create and approve pull requests".
> **Avalonia 12.0.1 + ReactiveUI.Avalonia 12.0.1 + ReactiveUI 23.2.1**.
> **FluentAssertions 8.9.0** — Xceed license accepted for this project.
> Remaining: response curve editor (axes), condition-based action pipeline.


## Project Overview

JoystickGremlinSharp is a **C# (.NET 10) rewrite** of JoystickGremlin — a Windows application
for configuring joystick/gamepad devices. It maps physical device inputs to virtual outputs via
vJoy, supporting macros, modes, response curves, and condition-based action pipelines.

- **Platform**: Windows (vJoy/DILL are Windows-only drivers); Avalonia UI enables future portability
- **UI Framework**: [Avalonia](https://avaloniaui.net/) 11.x with ReactiveUI MVVM
- **Device I/O**: P/Invoke to `vJoyInterface.dll` (virtual output) and `dill.dll` (physical input)


## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| UI | Avalonia 11.x |
| MVVM / Reactive | ReactiveUI + Avalonia.ReactiveUI |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Microsoft.Extensions.Logging + Serilog |
| Serialization | System.Text.Json |
| Native interop | P/Invoke (`vJoyInterface.dll`, `dill.dll`) |
| Testing | xUnit + Moq + FluentAssertions |


## Build, Lint, and Test Commands

```powershell
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run a specific test project
dotnet test tests/JoystickGremlin.Core.Tests

# Run the application
dotnet run --project src/JoystickGremlin.App

# Restore packages
dotnet restore
```


## Solution Structure

```
JoystickGremlinSharp.sln
src/
  JoystickGremlin.Interop/          net10.0-windows   P/Invoke wrappers for vJoy and DILL native DLLs
  JoystickGremlin.Core/             net10.0           Domain logic — devices, events, profile, modes, actions
  JoystickGremlin.App/              net10.0-windows   Avalonia MVVM app entry point + all UI
tests/
  JoystickGremlin.Core.Tests/       net10.0           xUnit tests for Core domain logic

# Python reference (do not modify):
gremlin/          Original Python core logic
action_plugins/   Original Python action plugins
vjoy/             Original vJoy ctypes wrapper (porting reference)
dill/             Original DILL ctypes wrapper (porting reference)
```

### Project Responsibilities

| Project | Purpose |
|---|---|
| `JoystickGremlin.Interop` | P/Invoke declarations for `vJoyInterface.dll` and `dill.dll`. Implements interfaces defined in Core. No business logic. |
| `JoystickGremlin.Core` | All domain logic. Defines interfaces (`IPhysicalDevice`, `IVirtualDevice`, `IEventPipeline`, etc.) consumed by both Interop and App. No Avalonia/UI references. |
| `JoystickGremlin.App` | Avalonia `Application`, `MainWindow`, all ViewModels and Views. Wires up DI. References Core and Interop. |
| `JoystickGremlin.Core.Tests` | xUnit tests for Core. Uses Moq to mock `IPhysicalDevice`, `IVirtualDevice`, etc. |

### Core Folder Structure

```
src/JoystickGremlin.Core/
  Actions/          IActionDescriptor, IActionFunctor, ActionRegistry, built-in descriptors:
                      VJoy/   VJoyAxisActionDescriptor, VJoyButtonActionDescriptor, VJoyHatActionDescriptor
                      Macro/  MacroActionDescriptor (tag: "macro"; fires key sequence on press/release)
                      ChangeMode/ ChangeModeActionDescriptor (tag: "change-mode"; switches active mode)
                      Keyboard/   IKeyboardSimulator, NullKeyboardSimulator (overridable by Interop)
  Configuration/    AppSettings, ISettingsService, JSON-backed settings store
  Devices/          IPhysicalDevice, IVirtualDevice, DeviceManager, device-info types
  Events/           InputEvent, EventPipeline, IEventProcessor, mode-aware routing
  Exceptions/       GremlinException and domain-specific exception types
  Modes/            ModeManager, Mode, mode-stack logic
  Profile/          Profile, InputBinding, ProfileRepository (JSON serialization)
                      IProfileState — singleton holding current Profile + FilePath, raises events
  Startup/          IStartupService, NullStartupService (overridable by Interop for real registry use)
  Common/           Extensions, utilities
```

### App Folder Structure

```
src/JoystickGremlin.App/
  Assets/           Icons, fonts, static resources
  Controls/         Custom Avalonia controls (reusable across views)
  ViewModels/       ReactiveObject-based ViewModels, one per View:
                      MainWindowViewModel  — nav bar, profile load/save, pipeline start/stop,
                                            CheckForUpdatesCommand (Velopack)
                      DevicesPageViewModel — lists physical devices from IDeviceManager
                      ProfilePageViewModel — current mode, mode switcher, profile metadata
                      SettingsPageViewModel — app settings via ISettingsService + IStartupService
                      BindingsPageViewModel — three-panel editor: device→input→bound actions
                      BoundActionViewModel  — wraps BoundAction; computes ConfigSummary
                      InputDescriptorViewModel — represents single axis/button/hat slot
  Views/            *.axaml Views, code-behind minimal
  FilePickerService.cs  — wraps Avalonia IStorageProvider; call SetTopLevel(mainWindow) before use
  App.axaml         Application definition — includes TrayIcon (Show / Exit context menu)
  App.axaml.cs      DI bootstrap, service registration, close-to-tray and start-minimized logic
  Program.cs        Entry point — VelopackApp.Build().Run() must be first line of Main()
```


## Coding Conventions

### File Headers

Every C# source file must include:
```csharp
// SPDX-License-Identifier: GPL-3.0-only
```

### Naming

| Element | Convention | Example |
|---|---|---|
| Classes, interfaces, enums | `PascalCase` | `DeviceManager`, `IPhysicalDevice` |
| Methods, properties | `PascalCase` | `GetDevices()`, `DeviceName` |
| Private fields | `_camelCase` | `_deviceManager`, `_logger` |
| Local variables, parameters | `camelCase` | `deviceGuid`, `inputEvent` |
| Constants | `PascalCase` | `MaxAxisValue` |
| Interfaces | `I` prefix | `IAction`, `IFunctor` |
| Async methods | `Async` suffix | `LoadProfileAsync`, `GetDevicesAsync` |

### XML Documentation

Add XML doc comments to all public classes and members:
```csharp
/// <summary>
/// Manages the collection of physical joystick devices detected by DILL.
/// </summary>
public sealed class DeviceManager : IDeviceManager
{
    /// <summary>
    /// Gets all currently connected physical devices.
    /// </summary>
    public IReadOnlyList<IPhysicalDevice> Devices => _devices;
}
```

### Nullable Reference Types

- Nullable reference types are **enabled** project-wide (`<Nullable>enable</Nullable>`)
- Use `?` for intentionally nullable references; **never suppress warnings with `!`** — always add an explicit null guard instead
- Initialize fields in constructors; use `required` init properties where appropriate
- In ViewModels, use `if (x is null) return;` pattern (not `x!`) before dereferencing potentially-null results (see `BindingsPageViewModel.AddAction` for the established pattern)

### Properties

- Use `{ get; private set; }` or `{ get; init; }` for immutable data:
```csharp
public Guid Id { get; init; }
public string Name { get; private set; }
```

### Error Handling

- Define exceptions in `Core/Exceptions/` inheriting from `GremlinException`
- Use specific types: `ProfileException`, `VJoyException`, `DeviceException`
- Catch exceptions at the highest relevant boundary and log before re-throwing or handling
```csharp
public sealed class ProfileException : GremlinException
{
    public ProfileException(string message) : base(message) { }
    public ProfileException(string message, Exception inner) : base(message, inner) { }
}
```

### Logging

- Inject `ILogger<T>` via constructor
- Use `LogTrace` for start/finish of operations; `LogWarning`/`LogError` for issues
- Use compile-time log source generators (`LoggerMessage.Define`) for hot paths
```csharp
private readonly ILogger<DeviceManager> _logger;

_logger.LogInformation("Initializing device {DeviceGuid}", device.Guid);
```

### Async/Await

- All I/O operations must be `async`/`await`
- Pass `CancellationToken cancellationToken` to all async calls where supported
- Never use `async void` except for event handlers

### Dependency Injection

- Register all services in `App.axaml.cs` via `IServiceCollection`
- Prefer constructor injection; never use `ServiceLocator` pattern
- Lifetimes: `Singleton` for managers/pipelines, `Transient` for actions/functors


## ReactiveUI & MVVM Patterns

### ViewModel Base

All ViewModels extend `ReactiveObject`:
```csharp
using ReactiveUI;

public sealed class MainWindowViewModel : ReactiveObject
{
    private string _title = "Joystick Gremlin";
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }
}
```

### Commands

Use `ReactiveCommand` for all UI commands:
```csharp
public ReactiveCommand<Unit, Unit> LoadProfileCommand { get; }

LoadProfileCommand = ReactiveCommand.CreateFromTask(LoadProfileAsync);
```

### Routing & Navigation

Use Avalonia's built-in `ContentControl` with ViewModel binding for view switching; prefer
`IScreen` + `RoutingState` from ReactiveUI for complex navigation trees.

### View Binding

Views bind to ViewModels via `DataContext`; use `{Binding}` in AXAML:
```xml
<TextBlock Text="{Binding Title}" />
<Button Command="{Binding LoadProfileCommand}" Content="Load" />
```


## P/Invoke Interop Patterns

### Structure

All P/Invoke declarations live in `JoystickGremlin.Interop`:
```
src/JoystickGremlin.Interop/
  VJoy/             VJoyNative.cs (DllImport), VJoyDevice.cs (IVirtualDevice impl)
  Dill/             DillNative.cs (DllImport), DillDevice.cs (IPhysicalDevice impl)
  Startup/          WindowsStartupService.cs (IStartupService — HKCU Run registry)
```

### DllImport Pattern

```csharp
internal static partial class VJoyNative
{
    private const string DllName = "vJoyInterface.dll";

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool vJoyEnabled();

    [LibraryImport(DllName)]
    internal static partial VjdStat GetVJDStatus(uint rID);
}
```

- Use `[LibraryImport]` (source-generated, .NET 7+) over legacy `[DllImport]`
- Mark P/Invoke classes `internal static partial`
- Wrap in a `sealed` implementation class that implements the Core interface

### Native DLL Deployment

The native DLLs (`vJoyInterface.dll`, `dill.dll`) from the Python source are copied to the
output directory via `<Content CopyToOutputDirectory="PreserveNewest">` in the `.csproj`.

### DILL Index and Value Conventions

- **InputIndex is 1-based** for all input types (buttons, axes, hats). Physical button 1 = `InputIndex=1`. Pass `data.InputIndex` directly as the event identifier — no offset adjustment needed. (The `NativeJoystickInputData` comment saying "Zero-based" is incorrect.)
- **Axis values** arrive as raw DirectInput signed-short integers (range −32768 to 32767). `DillDeviceManager` normalises to `[−1.0, 1.0]` via `Math.Clamp(data.Value / 32767.0, -1.0, 1.0)` before creating `InputEvent`.
- **AxisCount may be 0** from DILL even when axes exist (confirmed on MOZA R9 Base). `DillDevice` falls back to counting non-zero `AxisIndex` entries from `AxisMap`; DirectInput axis codes start at `0x30`, so `AxisIndex == 0` is the unused-slot sentinel.

### SendInput Struct Size (x64)

Windows `INPUT` struct on x64 is **40 bytes**: `DWORD type`(4) + padding(4) + union(32). The union must be forced to 32 bytes via `[StructLayout(LayoutKind.Explicit, Size=32)]`. Without this, `SendInput` silently returns 0 (`ERROR_INVALID_PARAMETER`). See `SendInputKeyboardSimulator.InputUnion`.


## Action System

Actions are **statically registered** (no runtime plugin discovery).

> **Note**: The codebase uses `IActionDescriptor` + `IActionFunctor` (not `IAction`/`IFunctor` as shown in the example below). The interfaces below are illustrative; see `Core/Actions/` for actual signatures.

### Built-in Action Descriptors

| Tag | Class | Config keys |
|---|---|---|
| `"vjoy-axis"` | `VJoyAxisActionDescriptor` | `deviceId`, `axisId` |
| `"vjoy-button"` | `VJoyButtonActionDescriptor` | `deviceId`, `buttonId` |
| `"vjoy-hat"` | `VJoyHatActionDescriptor` | `deviceId`, `hatId` |
| `"macro"` | `MacroActionDescriptor` | `keys` (comma-separated), `onPress` (bool) |
| `"change-mode"` | `ChangeModeActionDescriptor` | `targetMode` (string) |
| `"map-to-keyboard"` | `MapToKeyboardActionDescriptor` | `keys` (comma-separated key names), `behavior` ("Hold"/"Toggle"/"PressOnly"/"ReleaseOnly") |

### IKeyboardSimulator

`MacroActionDescriptor` depends on `IKeyboardSimulator` (Core abstraction). `NullKeyboardSimulator` is registered by default (no-op). The Interop layer can override with a real `SendInput` implementation by registering its own singleton **before** `AddCoreServices()` is called (uses `TryAddSingleton`).

### Interfaces

```csharp
// Core/Actions/IAction.cs
public interface IAction
{
    string Tag { get; }
    string Name { get; }
    InputType[] SupportedInputTypes { get; }
    IFunctor CreateFunctor();
}

// Core/Actions/IFunctor.cs
public interface IFunctor
{
    Task ProcessAsync(InputEvent inputEvent, CancellationToken cancellationToken);
}
```

### Registration

Register all built-in actions in `ActionRegistry` during DI setup:
```csharp
services.AddSingleton<IActionRegistry, ActionRegistry>();
services.AddTransient<IAction, MapToVJoyAction>();
services.AddTransient<IAction, MacroAction>();
services.AddTransient<IAction, ChangeModeAction>();
```


## Profile System

Profiles are stored as **JSON** via `System.Text.Json`:

```csharp
// Core/Profile/Profile.cs
public sealed class Profile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<Mode> Modes { get; init; } = [];
}

// Core/Profile/IProfileRepository.cs
public interface IProfileRepository
{
    Task<Profile> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(Profile profile, string path, CancellationToken cancellationToken = default);
}
```

Use `JsonSerializerOptions` with `WriteIndented = true` for human-readable profile files.


## Testing Conventions

- Tests go in `tests/JoystickGremlin.Core.Tests/`
- One test class per domain service/class, named `<Subject>Tests`
- Use `[Fact]` for single-case tests; `[Theory]` + `[MemberData]` for parameterized tests
- Use `Mock<T>` from Moq for dependencies; `Should()` assertions from FluentAssertions
- Arrange/Act/Assert structure with blank lines separating sections (no region comments)

```csharp
public sealed class ProfileRepositoryTests
{
    private readonly Mock<IFileSystem> _fileSystemMock = new();
    private readonly ProfileRepository _sut;

    public ProfileRepositoryTests()
    {
        _sut = new ProfileRepository(_fileSystemMock.Object);
    }

    [Fact]
    public async Task LoadAsync_ValidJson_ReturnsProfile()
    {
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(/* json */);

        var profile = await _sut.LoadAsync("test.json");

        profile.Should().NotBeNull();
        profile.Name.Should().Be("Test Profile");
    }
}
```


## Release Process

This project uses a **GitFlow release model**. Feature branches merge to `develop` freely.
When ready to ship, trigger a manual release workflow to bump the version and create an installer.

### Version Source

`version.json` (repo root) is the **single source of truth**:
```json
{ "version": "14.2.0" }
```
`Directory.Build.props` reads it at build time and injects `<Version>`, `<AssemblyVersion>`, and
`<FileVersion>` into all projects automatically — no per-project version tags needed.

### Triggering a Release

**Prerequisite**: The release workflow needs permission to create PRs. Configure ONE of:
- **`RELEASE_TOKEN` secret** (recommended): fine-grained PAT with **Contents: Read/write** +
  **Pull requests: Read/write** → Repo Settings → Secrets and variables → Actions → New secret
- **Repo setting** (alternative): Settings → Actions → General → Workflow permissions →
  enable **"Allow GitHub Actions to create and approve pull requests"**

1. Go to **GitHub → Actions → Release → Run workflow**
2. Select branch: **`develop`**
3. Choose **version_type**: `major` | `minor` | `patch`
4. Optionally add **release_notes** (supports markdown)
5. Click **Run workflow**

The workflow:
- Computes the new semver from `version.json`
- Creates `release/vX.Y.Z` branch from `develop`
- Bumps `version.json` and commits it
- Opens **PR → `main`** (title: *"Release vX.Y.Z"*)
- Opens **merge-back PR → `develop`** (title: *"chore: merge-back release vX.Y.Z"*)

### Completing the Release

1. **Review + merge the PR into `main`** — this triggers `publish.yml` automatically
2. `publish.yml` builds a self-contained `win-x64` binary, runs `vpk pack`, and creates a
   GitHub Release `vX.Y.Z` with the installer artifact
3. **Review + merge the merge-back PR into `develop`** to keep `version.json` in sync

### Building Locally

```powershell
# From repo root (requires vpk CLI and .NET 10 SDK)
.\installer\build-installer.ps1
# Output: installer/out/  — setup EXE + delta packages
```

### CI Workflows Summary

| Workflow | File | Trigger | Purpose |
|---|---|---|---|
| .NET CI | `dotnet-ci.yml` | Push/PR → develop, main | Build + test gate |
| Release | `release.yml` | Manual dispatch on develop | Bump version, open PRs |
| Publish | `publish.yml` | Push to main | Build installer, create GitHub Release |


## Pre-commit Checks

Before considering a task complete, run:

```powershell
dotnet build
dotnet test
```

> Current baseline: **131 tests, 0 failures** (as of `features/installer-systemtray`).


## GitHub Workflow

The project uses SSH for git push (`git@github.com:DevilDogTG/JoystickGremlinSharp.git`).

For PR creation and GitHub API operations, authenticate `gh` CLI:
```bash
# One-time setup per session
gh auth login --with-token   # paste a fine-grained PAT when prompted
gh repo set-default DevilDogTG/JoystickGremlinSharp

# Create PR
gh pr create --base develop --head <branch> --title "..." --body "..."
```

**Fine-grained PAT minimum permissions** (repository: `JoystickGremlinSharp` only):
- Metadata: Read-only
- Contents: Read-only
- Pull requests: Read and write
