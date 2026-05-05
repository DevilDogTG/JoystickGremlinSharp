# EmuWheel Configuration Guide

EmuWheel lets a vJoy device impersonate a supported steering wheel so games that whitelist known wheel VID/PID pairs can detect it as a wheel instead of a generic virtual joystick.

## What EmuWheel does

When a profile contains `emuwheel-*` actions and **Settings -> Wheel Emulation -> Enabled** is turned on, JoystickGremlinSharp:

1. Uses the configured vJoy slot for EmuWheel output.
2. Spoofs that slot's USB VID/PID to match the selected wheel model.
3. Restores the original vJoy identity when the pipeline stops.

The spoof is profile-scoped. If the app exits unexpectedly, the next startup attempts to restore the original vJoy identity automatically.

## Requirements

- Windows
- vJoy installed and configured
- Run JoystickGremlinSharp as **Administrator** if you want wheel spoofing to work
- Use a **dedicated vJoy slot** for EmuWheel, separate from your normal generic vJoy mappings

> If the app is not running as Administrator, EmuWheel actions can still write to vJoy, but the slot keeps the normal vJoy identity. Games that require a recognized wheel VID/PID may not detect it as a wheel.

## Supported wheel identities

- Logitech G29
- Logitech G920
- Thrustmaster T300RS
- Thrustmaster TMX

Choose the model your target game is most likely to recognize.

## Step 1: Configure vJoy

Open **vJoyConf** and configure a dedicated device slot for EmuWheel. The default slot in JoystickGremlinSharp is **Device 2**.

Recommended starting configuration:

- **vJoy Device:** 2
- **Axes:** enable `X`, `Y`, `Z`, `Rx`, `Ry`, `Rz`
- **Buttons:** enable as many as you need, for example `32`
- **POV Hat Switch:** enable `Continuous` with at least `1` POV if you want a D-pad / hat
- **Force Feedback:** optional and unrelated to wheel detection

### Important: axis numbers are standard vJoy axes

EmuWheel does **not** target the extra labels shown in vJoyConf such as **Wheel**, **Accelerator**, **Brake**, **Clutch**, or **Steering**.

It writes to the normal vJoy axis numbers:

| EmuWheel `axisIndex` | vJoy axis |
|---|---|
| 1 | X |
| 2 | Y |
| 3 | Z |
| 4 | Rx |
| 5 | Ry |
| 6 | Rz |
| 7 | Slider |
| 8 | Dial/Slider2 |

So if an EmuWheel binding uses `vjoyId = 2` and `axisIndex = 1`, that means **vJoy Device 2 -> X axis**.

### Suggested wheel-style mapping

One simple convention is:

| Function | EmuWheel target |
|---|---|
| Steering | `axisIndex = 1` (`X`) |
| Throttle | `axisIndex = 2` (`Y`) or `6` (`Rz`) |
| Brake | `axisIndex = 3` (`Z`) |
| Clutch | `axisIndex = 4` (`Rx`) or `5` (`Ry`) |

The only hard rule is that the axis must be enabled in **vJoyConf** and the same axis number must be used in the EmuWheel action configuration.

## Step 2: Configure JoystickGremlinSharp settings

In **Settings -> Wheel Emulation**:

1. Turn on **Enabled**
2. Set **EmuWheel vJoy Device ID** to the same slot you configured in vJoyConf
3. Select a **Wheel Model**

Notes:

- The EmuWheel vJoy slot should be different from your generic vJoy device ID.
- For some games, especially **The Crew 2** and **Forza Horizon 4**, disable **Steam Input** for that game.

## Step 3: Add EmuWheel actions to a profile

EmuWheel actions use these tags:

- `emuwheel-axis`
- `emuwheel-button`
- `emuwheel-hat`

This branch includes the EmuWheel backend and action descriptors, but it does **not** yet expose dedicated EmuWheel config fields in the binding editor. For now, the reliable way to assign non-default axis, button, and hat targets is to edit the profile JSON directly.

## Profile JSON examples

Profile files are stored as JSON with PascalCase property names.

### Axis example

This maps a physical axis to **vJoy Device 2 -> X axis**:

```json
{
  "ActionTag": "emuwheel-axis",
  "Configuration": {
    "vjoyId": 2,
    "axisIndex": 1
  }
}
```

### Button example

This maps a physical button to **vJoy Device 2 -> Button 1**:

```json
{
  "ActionTag": "emuwheel-button",
  "Configuration": {
    "vjoyId": 2,
    "buttonIndex": 1
  }
}
```

For analog source inputs, `emuwheel-button` also supports an optional threshold:

```json
{
  "ActionTag": "emuwheel-button",
  "Configuration": {
    "vjoyId": 2,
    "buttonIndex": 1,
    "threshold": 0.5
  }
}
```

### Hat example

This maps a physical hat to **vJoy Device 2 -> POV 1**:

```json
{
  "ActionTag": "emuwheel-hat",
  "Configuration": {
    "vjoyId": 2,
    "hatIndex": 1
  }
}
```

### Full binding example

```json
{
  "Name": "EmuWheel Profile",
  "Bindings": [
    {
      "DeviceGuid": "11111111-1111-1111-1111-111111111111",
      "InputType": "JoystickAxis",
      "Identifier": 1,
      "Actions": [
        {
          "ActionTag": "emuwheel-axis",
          "Configuration": {
            "vjoyId": 2,
            "axisIndex": 1
          }
        }
      ]
    },
    {
      "DeviceGuid": "11111111-1111-1111-1111-111111111111",
      "InputType": "JoystickAxis",
      "Identifier": 2,
      "Actions": [
        {
          "ActionTag": "emuwheel-axis",
          "Configuration": {
            "vjoyId": 2,
            "axisIndex": 3
          }
        }
      ]
    }
  ]
}
```

In that example:

- physical axis 1 drives steering on `X`
- physical axis 2 drives brake on `Z`

## Defaults

If an EmuWheel action is created without explicit configuration, the backend defaults are:

- `vjoyId = 2`
- `axisIndex = 1`
- `buttonIndex = 1`
- `hatIndex = 1`
- `threshold = 0.5` for `emuwheel-button`

## Troubleshooting

### Game does not detect the wheel

- Run JoystickGremlinSharp as **Administrator**
- Verify the profile actually contains `emuwheel-*` actions
- Make sure **Settings -> Wheel Emulation -> Enabled** is on
- Make sure the configured EmuWheel slot exists in vJoyConf
- Try a different supported wheel model

### Inputs move in JoystickGremlinSharp but not in game

- Confirm the profile is running
- Confirm the profile points to the same `vjoyId` as the EmuWheel settings
- Confirm the target axis is enabled in vJoyConf
- Disable Steam Input for the game if it interferes with wheel detection

### Axis appears on the wrong control

Check the `axisIndex` mapping table above. EmuWheel uses standard vJoy numbering, not the extra wheel/pedal labels shown in vJoyConf.
