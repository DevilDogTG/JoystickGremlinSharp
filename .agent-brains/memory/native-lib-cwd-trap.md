# Memory: Bundled native libs that write to CWD

## Lesson

When this project (or any project) ships a third-party native DLL via `<Content
CopyToOutputDirectory>` and the DLL writes diagnostic/log files (e.g. a
`*_debug.log` next to itself), those writes go to the **current working
directory** at `init()` time — not to the DLL's own directory.

Result: the app appears to work in `dotnet run` (CWD = `bin/Debug/...`, writable
by the dev user), but **crashes hard for non-admin users on the installed
build** — because CWD is `C:\Program Files\<app>\` (MSI shortcut) or
`C:\Windows\system32` (HKCU `Run` registry launch at boot), and neither is
writable by a normal user. The native code's `fopen`-then-write pattern
typically faults the process before any managed exception fires, leaving no
Serilog entry past the last successful log line.

## Rule for this project

Before invoking the native `init()` of any bundled DLL that may write to CWD,
**relocate `Environment.CurrentDirectory` to a per-user-writable folder and
restore it in `finally`**.

Reference implementation in this repo:
`src/JoystickGremlin.Interop/Dill/DillDeviceManager.InitializeNative` —
redirects CWD to `%LOCALAPPDATA%\JoystickGremlinSharp\dill\` around
`DillNative.init()`. See `AGENTS.md` → *Native DLL Deployment* for the
project-level statement of this rule.

## How to detect a candidate

- The DLL's source repository or release notes mention a debug-log file (search
  for `_debug.log`, `_trace.log`, or anything similar in the source).
- The `.gitignore` already excludes a stray `*_debug.log` near a `bin/Debug`
  directory (clue that someone has already noticed the file appearing).
- The app crashes in installed builds for non-admin users but works in
  `dotnet run`, and the most recent log line is right before the native
  P/Invoke.

## Why this is a memory, not a rule

The exact mitigation (per-user CWD relocation) is project-specific because the
target folder depends on the app's data-root convention. Cross-project rule
would be "audit native DLL CWD-writes before bundling"; this file documents the
*pattern + the example* so future contributors recognize it on sight.

## Related

- Symptom report and root-cause analysis:
  `.agent-brains/plan/fix-startup-crash-dill-cwd.md`
- Implementation:
  `src/JoystickGremlin.Interop/Dill/DillDeviceManager.cs`
- Defensive diagnostics added at the same time (so a future silent crash is
  visible): `src/JoystickGremlin.App/Program.cs` (`AppDomain.UnhandledException`
  + `TaskScheduler.UnobservedTaskException` handlers).
