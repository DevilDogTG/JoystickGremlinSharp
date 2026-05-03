# AGENTS.md - JoystickGremlinSharp Developer Guide

This file provides guidance for AI agents working on the JoystickGremlinSharp codebase.

> **Migration note**: The original Python/PySide6/QML source (under `gremlin/`, `action_plugins/`,
> `vjoy/`, `dill/`) is retained as a **reference implementation** only. All new development
> targets the C# solution described in this file.
> Legacy folders `doc/`, `gfx/`, and `gh_page/` have been removed (Python Sphinx docs and
> GitHub Pages from the original project — not applicable to the C# rewrite).

> **Status**: Phase complete. All core features implemented and released.
> - Release v10.0.3 published with auto-generated release notes
> - Workflow: main-first + tag-based release (no merge-back)
> - 173 tests passing, 0 build warnings
> - Latest: Multi-button to virtual output mapping (buttons-to-hat, buttons-to-axes actions)
> - GitHub Actions permissions must be set to "Allow all actions and reusable workflows"
>   (Settings → Actions → General) for workflows to run on `main`
> - Release pipeline requires either `RELEASE_TOKEN` secret (fine-grained PAT: Contents+PRs write)
>   OR repo setting: Settings → Actions → General → "Allow GitHub Actions to create and approve pull requests"
> - **Skills**: Updated code-review (C#/.NET/Avalonia), new finish-feature (automated release workflow)
> - **Remaining optional features**: response curve editor (axes), condition-based action pipeline, UI for button mapping configuration


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
# Build the solution (Debug mode)
dotnet build

# Build with Release config (matches CI gate -warnaserror)
dotnet build --configuration Release -warnaserror

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
                                            CheckForUpdatesCommand (Velopack);
                                            AvailableModeEntries + SelectedModeEntry for mode ComboBox
                      DevicesPageViewModel — lists physical devices from IDeviceManager
                      ProfilePageViewModel — mode list (DFS tree order), add/remove/edit mode,
                                            parent-mode selection (AvailableParentNames rebuilt before EditParentName is set)
                      SettingsPageViewModel — app settings via ISettingsService + IStartupService
                      BindingsPageViewModel — mode selector (independent of runtime mode), inherited bindings,
                                             auto-apply config (debounced 300ms)
                      BoundActionViewModel  — wraps BoundAction; computes ConfigSummary; tracks InheritedFromMode
                      InputDescriptorViewModel — represents single axis/button/hat slot
                      ModeViewModel         — wraps Mode for Profile page; Depth + TreePadding (left-indent only)
                      ModeTreeEntry         — record(Name, IndentedLabel) for toolbar ComboBox tree display
                      ModeTreeHelper        — static: Flatten() DFS traversal + BuildEntries() for both ViewModels
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
                    VJoyNativeLibraryLoader.cs — SetDllImportResolver; prefers installed DLL
                    VJoyRegistryHelper.cs — shared helper to read vJoy install dir from registry
                    VJoyPrerequisiteChecker.cs — startup check (no P/Invoke): IsInstalled/IsCompatible
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

`dill.dll` is bundled in the repository (`src/JoystickGremlin.Interop/Dill/dill.dll`) and copied to
the output directory via `<Content CopyToOutputDirectory="PreserveNewest">` in the `.csproj`.

**vJoy is a prerequisite — not bundled (functional), install separately.**
The app ships a reference copy of `vJoyInterface.dll` v2.2.2.0 for dependency resolution, but at
runtime `VJoyNativeLibraryLoader` always prefers the DLL from the user's vJoy installation so the
SDK and kernel driver versions match.

#### Required vJoy version
- **Fork**: [BrunnerInnovation/vJoy](https://github.com/BrunnerInnovation/vJoy) v2.2.x or later
- **Download**: https://github.com/BrunnerInnovation/vJoy/releases
- The app checks this requirement at startup (`VJoyPrerequisiteChecker`) and shows a warning
  dialog with the download link if vJoy is absent or an incompatible version is found.

#### vJoy DLL loading strategy (`VJoyNativeLibraryLoader`)
`NativeLibrary.SetDllImportResolver` is registered once (before any P/Invoke) to redirect
`vJoyInterface.dll` loads to `<InstallDir>\x64\vJoyInterface.dll`. The install directory is read
from the Windows uninstall registry (`HKLM\...\Uninstall`, also checks `WOW6432Node`). Falls back
to the bundled DLL if the installed path cannot be found.

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
| `"vjoy-axis"` | `VJoyAxisDescriptor` | `vjoyId` (uint, default 1), `axisIndex` (int, default 1) |
| `"vjoy-button"` | `VJoyButtonDescriptor` | `vjoyId` (uint, default 1), `buttonIndex` (int, default 1) |
| `"vjoy-hat"` | `VJoyHatDescriptor` | `vjoyId` (uint, default 1), `hatIndex` (int, default 1) |
| `"buttons-to-hat"` | `ButtonsToHatDescriptor` | `vjoyId` (uint, default 1), `hatIndex` (int, default 1), `upButtonId`, `downButtonId`, `leftButtonId`, `rightButtonId` (int) |
| `"buttons-to-axes"` | `ButtonsToAxesDescriptor` | `vjoyId` (uint, default 1), `xAxisIndex` (int, default 1), `yAxisIndex` (int, default 2), `upButtonId`, `downButtonId`, `leftButtonId`, `rightButtonId` (int) |
| `"macro"` | `MacroActionDescriptor` | `keys` (comma-separated), `onPress` (bool, default true) |
| `"change-mode"` | `ChangeModeActionDescriptor` | `targetMode` (string) |
| `"map-to-keyboard"` | `MapToKeyboardActionDescriptor` | `keys` (comma-separated key names), `behavior` ("Hold"/"Toggle"/"PressOnly"/"ReleaseOnly", default "Hold") |

### Multi-Button to Virtual Output Mapping

**New actions** (v10.1+) enable stateful mapping of 4 physical buttons (Up/Down/Left/Right) to virtual outputs:

#### `buttons-to-hat` (D-Pad Mapping)

Maps four physical buttons to a single vJoy Hat/POV output with modal state tracking:
- **Button state machine**: Tracks which of the 4 buttons are currently pressed
- **Hat output**: Calculates 360° directional output (0° = Up, 90° = Right, 180° = Down, 270° = Left, diagonals at 45° intervals)
- **Center (-1)**: When no buttons are pressed, opposite directions pressed (e.g., Up+Down), or all four pressed
- **State sharing**: Multiple button bindings to the same vJoy hat share state (keyed by vjoyId:hatIndex)

**Configuration example**:
```json
{
  "actionTag": "buttons-to-hat",
  "configuration": {
    "vjoyId": 1,
    "hatIndex": 1,
    "upButtonId": 5,
    "downButtonId": 6,
    "leftButtonId": 7,
    "rightButtonId": 8
  }
}
```

**Setup**: Create 4 separate input bindings (one for each physical button) with the same action config. The functor will recognize each button and update shared state accordingly.

#### `buttons-to-axes` (Analog Stick Mapping)

Maps four physical buttons to dual vJoy axes (X/Y) with coordinated state tracking:
- **Button state machine**: Tracks all 4 button states (shared across Up/Down/Left/Right)
- **Y-axis output**: Up → +1.0, Down → -1.0, Up+Down → 0.0
- **X-axis output**: Right → +1.0, Left → -1.0, Left+Right → 0.0
- **Atomic updates**: Both axes written together when state changes (no intermediate values visible to games)
- **State sharing**: Multiple button bindings to the same vJoy axes share state (keyed by vjoyId:xAxisIndex:yAxisIndex)

**Configuration example**:
```json
{
  "actionTag": "buttons-to-axes",
  "configuration": {
    "vjoyId": 1,
    "xAxisIndex": 1,
    "yAxisIndex": 2,
    "upButtonId": 5,
    "downButtonId": 6,
    "leftButtonId": 7,
    "rightButtonId": 8
  }
}
```

**Setup**: Same as buttons-to-hat — create 4 bindings, one per button, all referencing the same action config.

#### Implementation Details

- **State storage**: Per-descriptor class-level dictionaries (keyed by vjoyId:indices) maintain state across multiple functors
- **Thread safety**: Locked state updates prevent race conditions (important for 1000 Hz polling)
- **Threshold**: Values ≥0.5 treated as pressed; <0.5 treated as released (standard joystick button convention)
- **Tested**: All 16 button state permutations tested for both Hat and Axes (ButtonsToHatFunctorTests, ButtonsToAxesFunctorTests)

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


## Profile Hierarchy & Inheritance

Modes form a **tree hierarchy** within a profile, enabling inheritance of bindings to reduce duplication.

### Hierarchy Model

- Each `Mode` has an optional `ParentModeName` (string?, null = root mode)
- Multiple root modes are allowed (forest, not strict tree)
- Circular references are prevented at validation time
- At runtime, modes form an **active mode stack** via `IModeManager`

### Binding Resolution

When resolving input bindings for an active mode:

1. Check current mode for matching binding (device + input match)
2. If not found, walk up the inheritance chain via `GetInheritanceChain(modeName, profile)`
3. Stop at **first matching binding** — inherits the action from ancestor
4. If no ancestor has it, the input fires no action (silent pass-through)

**Key semantics**:
- No merge; single binding per input slot per mode lineage
- First-match-wins (child always shadows parent, even if child binding is intentionally "empty")
- Parent rename orphans children (no automatic name tracking); deletion orphans children (no cascade)

### Bindings Page UX

The Bindings page (App/Views/BindingsPageView.axaml) includes three UX enhancements for hierarchy editing:

| Feature | Description |
|---|---|
| **Mode Selector** | Independent `ComboBox` (AvailableEditModeEntries) lets users edit any mode without changing the runtime active mode. Decoupled from IModeManager.ActiveModeName. |
| **Inherited Bindings Display** | RebuildBoundActions walks the inheritance chain and marks actions with `InheritedFromMode` (string?). Child actions shown as read-only "(↑ From: ParentName)" entries, muted opacity. |
| **Override Button** | When an inherited action is selected, "Override in this mode" button copies the action into the current edit mode, removing inheritance. |
| **Auto-Apply Config** | Config changes auto-save via Observable.Merge throttled at 300ms (no manual Apply button). |


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

This project uses a **main-first, tag-based release model**. Feature branches are created from
`main`, rebased onto `main` before review, and merged using **rebase merge** (linear history, no
merge commits). When ready to ship, trigger the manual release workflow.

### Branch Strategy

```
main          ← primary branch; all feature PRs target here
feature/xyz   ← branch from main; rebase on main before opening PR; rebase-merge into main
release/vX.Y.Z ← short-lived; created by release.yml; merged into main via rebase-merge PR
```

- **No `develop` branch** — `develop` is kept as a frozen legacy/archive reference only.
- **No merge-back** — `main` is the only long-lived branch; there is nothing to sync back to.
- **Rebase merge enforced** — merge commits and squash merges are disabled at the repo level
  (Settings → General → Pull Requests).

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
2. Select branch: **`main`**
3. Choose **version_type**: `major` | `minor` | `patch`
4. Optionally add **release_notes** (supports markdown)
5. Click **Run workflow**

The workflow:
- Computes the new semver from `version.json`
- Creates `release/vX.Y.Z` branch from `main`
- Bumps `version.json` and commits it
- Opens **PR → `main`** (title: *"Release vX.Y.Z"*)

### Completing the Release

1. **Review + rebase-merge the PR into `main`** — this triggers `tag.yml` automatically
2. `tag.yml` reads `version.json` from `main` and creates + pushes the `vX.Y.Z` git tag
3. `publish.yml` triggers on the tag, builds a self-contained `win-x64` binary, runs `vpk pack`,
   renames the installer to `JoystickGremlinSharp-{version}-Setup.exe`, and creates a GitHub
   Release with auto-generated release notes and the versioned installer as the only asset

### Hotfix Process

Use a hotfix when `main` has a bug that needs an immediate patch release without waiting for
the normal feature cycle.

```
hotfix/description  ──fix──►  PR → main  ──rebase-merge──►  release.yml (patch) ──►  vX.Y.(Z+1)
```

1. **Create a hotfix branch from `main`** (or from the specific tag if main has moved on):
   ```powershell
   git fetch origin
   git checkout -b hotfix/description origin/main
   # OR from a specific tag:
   git checkout -b hotfix/description v10.1.2
   ```
2. **Implement the fix** — commit with `fix:` prefix; run `dotnet build && dotnet test`
3. **Open PR → `main`** — same process as any feature PR:
   ```powershell
   git push -u origin hotfix/description
   & "C:\Program Files\GitHub CLI\gh.exe" pr create --base main --head hotfix/description --title "fix: <description>" --body "..."
   ```
4. **Rebase-merge the PR into `main`** — the fix is now live on `main`
5. **Trigger a patch release** — go to **Actions → Release → Run workflow**, select `main`,
   choose `patch`, then follow the normal [Completing the Release](#completing-the-release) steps

> **Note**: `tag.yml` only fires for `release/*` PRs, not `hotfix/*`. Always trigger `release.yml`
> after merging a hotfix to create the proper patch release tag + installer.

### Building Locally

```powershell
# From repo root (requires vpk CLI and .NET 10 SDK)
.\installer\build-installer.ps1
# Output: installer/out/  — setup EXE + delta packages
```

### CI Workflows Summary

| Workflow | File | Trigger | Purpose |
|---|---|---|---|
| .NET CI | `dotnet-ci.yml` | Push/PR → main | Build + test gate |
| Release | `release.yml` | Manual dispatch on main | Bump version, open PR |
| Tag Release | `tag.yml` | Release PR merged → main | Create vX.Y.Z git tag |
| Publish | `publish.yml` | Push tag `v*` | Build installer, create GitHub Release |

### GitHub Actions Git Auth

All workflows that perform `git push` (release.yml, tag.yml) must include explicit `GH_TOKEN` environment variable in the step. This ensures git operations succeed with proper authentication — the token from `actions/checkout` doesn't persist reliably for subsequent shell operations.

**Correct pattern:**
```yaml
- name: Push tag/branch
  env:
    GH_TOKEN: ${{ secrets.RELEASE_TOKEN || secrets.GITHUB_TOKEN }}
  run: git push origin <ref>
```

### RELEASE_TOKEN Setup

For the release pipeline to work end-to-end, `RELEASE_TOKEN` must be configured:

1. Create fine-grained PAT at: https://github.com/settings/personal-access-tokens/new
   - **Repository access**: `JoystickGremlinSharp` only
   - **Permissions**: Contents (read/write), Pull requests (read/write)
2. Store in repo: Settings → Secrets and variables → Actions → New secret
   - **Name**: `RELEASE_TOKEN`
   - **Secret**: Paste the token
3. Workflows fall back to `GITHUB_TOKEN` if `RELEASE_TOKEN` is missing, but may have limited permissions


## AI Skills & Workflows

Specialized skills are available in `.claude/commands/` to automate common tasks:

| Skill | Purpose | Usage |
|---|---|---|
| `code-review` | Structured code review for C#/.NET/Avalonia code. Checks ReactiveUI patterns, XAML bindings, threading, C# correctness, Avalonia conventions. Surfaces CRITICAL (crashes, deadlocks, data loss), WARNING (perf, memory, maintenance), and STYLE (idiom violations). | Invoke when PR code needs review or before pushing |
| `finish-feature` | Automates finalization of a feature branch: commit → push → PR → code review → summary. Five-step workflow for release-ready code. | Use after completing feature implementation |

**Skill locations**:
- `.claude/commands/code-review.md` — C#/.NET/Avalonia code review checklist
- `.claude/commands/finish-feature.md` — Release workflow automation


## Pre-commit Checks

Before considering a task complete, run:

```powershell
dotnet build --configuration Release -warnaserror
dotnet test
```

> Current baseline: **135 tests, 0 failures, 0 build warnings**.



## GitHub Workflow

The project uses SSH for git push (`git@github.com:DevilDogTG/JoystickGremlinSharp.git`).

For PR creation and GitHub API operations, authenticate `gh` CLI:
```bash
# One-time setup per session
gh auth login --with-token   # paste a fine-grained PAT when prompted
gh repo set-default DevilDogTG/JoystickGremlinSharp

# Create PR (always targeting main)
gh pr create --base main --head <branch> --title "..." --body "..."
```

**Fine-grained PAT minimum permissions** (repository: `JoystickGremlinSharp` only):
- Metadata: Read-only
- Contents: Read-only
- Pull requests: Read and write
