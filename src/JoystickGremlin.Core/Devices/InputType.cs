// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Represents the type of input on a physical or virtual joystick device.
/// </summary>
public enum InputType
{
    /// <summary>A digital button (pressed/released).</summary>
    JoystickButton,

    /// <summary>An analog axis in the range [-1.0, 1.0].</summary>
    JoystickAxis,

    /// <summary>A hat/POV switch reporting a directional position.</summary>
    JoystickHat,

    /// <summary>A keyboard key event.</summary>
    Keyboard,

    /// <summary>A mouse button event.</summary>
    MouseButton,

    /// <summary>A mouse axis movement.</summary>
    MouseAxis,
}
