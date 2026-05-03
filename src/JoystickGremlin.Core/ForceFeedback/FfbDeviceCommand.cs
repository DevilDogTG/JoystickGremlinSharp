// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Device-level force feedback control commands.
/// </summary>
public enum FfbDeviceCommand
{
    /// <summary>Enable the actuators on the device.</summary>
    EnableActuators = 1,

    /// <summary>Disable the actuators on the device.</summary>
    DisableActuators = 2,

    /// <summary>Stop all currently playing effects.</summary>
    StopAll = 3,

    /// <summary>Reset the device to its default state.</summary>
    Reset = 4,

    /// <summary>Pause all currently playing effects.</summary>
    Pause = 5,

    /// <summary>Continue all paused effects.</summary>
    Continue = 6,
}
