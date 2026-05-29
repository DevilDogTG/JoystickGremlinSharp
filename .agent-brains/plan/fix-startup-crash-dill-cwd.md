# Plan: Fix "Start with Windows" crash & admin-required relaunch

**Status:** active
**Created:** 2026-05-29
**Branch:** fix/startup-crash-dill-cwd

## Goal

Stop the app from crashing for non-admin users on installed (MSI) builds, whether
launched manually from the Start Menu/desktop shortcut or via Windows startup.

## Root Cause (confirmed)

Bundled `dill.dll` writes `dill_debug.log` to the **current working directory**
during its `init()` call. After the WiX MSI move (ADR-0002) the CWD at launch is
either `C:\Program Files\JoystickGremlinSharp\` (shortcut launch) or
`C:\Windows\system32` (HKCU Run launch at boot) — neither is writable by a
non-admin user. The native file-open failure kills the process before any
managed exception can be caught. Confirmed by user's crash log ending at
`Initializing DILL device manager` and never reaching the next log line.

## Checklist

- [x] Investigate startup sequence & exception paths
- [x] Identify root cause (dill.dll writes to CWD)
- [x] Write plan
- [x] Create feature branch `fix/startup-crash-dill-cwd`
- [x] Part A — Set CWD to `%LOCALAPPDATA%\JoystickGremlinSharp\dill\` around `DillNative.init()` in `DillDeviceManager.Initialize()`
- [x] Part B1 — Add `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` handlers in `Program.Main`
- [x] Part B2 — Wrap `MainWindow.Opened` async lambda body in try/catch (`App.axaml.cs:64`)
- [x] Part B3 — Wrap `DillNative.init()` call in try/catch with clear `DeviceException`
- [x] Update `AGENTS.md` Native DLL Deployment section with CWD warning
- [x] Run `dotnet build` + `dotnet test` — clean build (0 warnings, 0 errors), 328 tests pass
- [ ] Open PR against `main`

## Files to modify

- `src/JoystickGremlin.Interop/Dill/DillDeviceManager.cs` (lines 56-74)
- `src/JoystickGremlin.App/Program.cs`
- `src/JoystickGremlin.App/App.axaml.cs` (lines 64-98)
- `AGENTS.md` (Native DLL Deployment section)

## Progress Log

_Updated as steps complete._

- 2026-05-29: Investigated crash. Crash log ends at "Initializing DILL device manager",
  confirming `DillNative.init()` as the crash point. Found `dill_debug.log` in
  `.gitignore` and dev `bin/Debug` — dill.dll is a known CWD-writer. Plan written.
- 2026-05-29: Branch `fix/startup-crash-dill-cwd` created from `main`.
- 2026-05-29: Implemented Part A (CWD relocation), Part B1-B3 (exception handlers + diagnostic
  try/catch), and the `AGENTS.md` note. Build clean, 328 tests pass.
