# HidHide Integration

[HidHide](https://github.com/nefarius/HidHide) is a Nefarius kernel-mode HID
filter driver that hides selected HID devices from all processes except an
explicit allow-list. JoystickGremlinSharp integrates with HidHide as a
first-class dependency so you can hide your physical wheel from racing games
while keeping it visible to JoystickGremlinSharp itself, presenting only the
virtual JGS Wheel to the game.

## What the app does

`IHidHideService` (Core abstraction) wraps the `HidHideCLI.exe` shipped with
HidHide. The default `NullHidHideService` is a safe no-op; the Interop layer
registers `HidHideCliService` in DI which detects the CLI at runtime via
common install paths (`C:\Program Files\Nefarius Software Solutions\HidHide`).

Operations:

| Operation | CLI flag | Description |
|---|---|---|
| `GetStatus` | n/a | Reports CLI presence + path |
| `ListDevicesAsync` | `--dev-list` | Enumerate all hideable HID devices |
| `HideDeviceAsync` | `--dev-hide <id>` | Add device instance path to hide list |
| `UnhideDeviceAsync` | `--dev-unhide <id>` | Remove from hide list |
| `ListWhitelistAsync` | `--app-list` | Enumerate process allow-list |
| `AddWhitelistEntryAsync` | `--app-reg <path>` | Add `.exe` to allow-list |
| `RemoveWhitelistEntryAsync` | `--app-unreg <path>` | Remove from allow-list |
| `SetCloakEnabledAsync` | `--cloak-on` / `--cloak-off` | Master gate |

> **Note**: CLI flags assume HidHide ≥ 1.5. If your version uses different
> flags the operations will surface as exceptions; please file an issue.

## Settings

Two settings in `AppSettings` control the integration:

- `EnableHidHide` (default `false`) — master enable.
- `HidHideCliPath` (default empty → auto-detect) — explicit CLI path.

## Per-profile hiding (planned UI)

The forthcoming UI will expose:

- **Settings → HidHide card**: install status, master enable, CLI path picker,
  device-by-device hide list, per-app whitelist editor.
- **Profile editor**: per-profile `HideDeviceInstanceIds` and
  `HidHideWhitelistApps` lists so a Forza profile can hide your MOZA but
  keep it visible to your sim-rig dashboard app.

Until those views land, `IHidHideService` can be invoked from custom action
plugins or extensions.
