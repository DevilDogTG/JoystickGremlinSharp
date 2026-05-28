# Project Overview

## JoystickGremlinSharp — C# rewrite of JoystickGremlin

## Key Decisions

- **ADR-0001** (2026-05-25): `asInvoker` manifest + on-demand UAC for HidHide CLI subprocesses.
  See `.agent-brains/decisions/ADR-0001-on-demand-uac-for-hidhide-cli.md`.


**Branch model**: main-first, tag-based releases. Feature branches → rebase-merge PR → main.

**Test baseline**: 319 tests, 0 warnings (as of `feature/autoload-rework` merged, released v10.6.0, 2026-05-28).

---

## HidHide Integration (feature/hidhide-startup-ux)

HidHide is an optional device-hiding driver by Nefarius. Our integration:
- **Auto-whitelist**: App whitelists its own exe at startup so it can see devices HidHide may be hiding.
- **On-demand UAC**: App runs as `asInvoker` (no forced admin). If CLI write needs elevation, Windows UAC prompt appears just for that subprocess. User can decline safely.
- **Toolbar button**: `🛡 HidHide` opens native HidHide configuration client. Grayed out when not installed.
- **Startup check**: `PrerequisitesWarningDialog` shown if vJoy or HidHide is absent/incompatible.
- **No in-app config page**: Device hiding configuration fully delegated to the native HidHide client.

## Remaining Optional Features

- Response curve editor (axes)
- Condition-based action pipeline
- UI for button mapping configuration
