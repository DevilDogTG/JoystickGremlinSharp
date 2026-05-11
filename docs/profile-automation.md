# Profile Automation

JoystickGremlinSharp can automatically activate a profile when a target game
launches and deactivate it when the game exits. This is implemented by the
process-monitor service (`IProcessMonitor` + `ProcessProfileResolver`) which
watches running processes and resolves them against per-process mappings.

## Settings

| Key | Default | Purpose |
|---|---|---|
| `EnableAutoLoading` | `false` | Master enable for automation |
| `ProcessMappings` | `[]` | List of `{ ExecutableName, ProfileFilePath }` |

## Mapping shape

```json
{
  "processMappings": [
    {
      "executableName": "ForzaHorizon5.exe",
      "profileFilePath": "C:\\Users\\me\\AppData\\Roaming\\JoystickGremlinSharp\\profiles\\racing\\forza5.json"
    },
    {
      "executableName": "AC2-Win64-Shipping.exe",
      "profileFilePath": "C:\\...\\profiles\\racing\\acc.json"
    }
  ]
}
```

The matcher is **case-insensitive** on `executableName` and matches on the
process image filename (no path). When a mapped process appears the matching
profile is loaded into `IProfileState` and the event pipeline restarts.

## Combining with EmuWheel

A typical racing-game profile combines:

- `PreferredBackendId = "jgs-wheel"` — force the JGS Wheel backend so the game
  sees a recognised racing wheel.
- `HideDeviceInstanceIds` — hide the physical wheel from the game.
- `HidHideWhitelistApps` — allow JoystickGremlinSharp itself to see the wheel.
- `ProcessMappings[ExecutableName]` — auto-activate when the game launches.

This combination is documented end-to-end in
[`jgs-wheel-driver.md`](jgs-wheel-driver.md) and
[`hidhide-integration.md`](hidhide-integration.md).
