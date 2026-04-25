// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Represents a virtual joystick device managed by the vJoy driver.
/// </summary>
public interface IVirtualDevice
{
    /// <summary>Gets the vJoy device ID (1-based).</summary>
    uint DeviceId { get; }

    /// <summary>Sets an axis value in the range [-1.0, 1.0].</summary>
    void SetAxis(int axisIndex, double value);

    /// <summary>Sets a button state.</summary>
    void SetButton(int buttonIndex, bool pressed);

    /// <summary>Sets a hat/POV direction in degrees (0–35999), or -1 for center.</summary>
    void SetHat(int hatIndex, int degrees);

    /// <summary>Resets all inputs on the device to their neutral state.</summary>
    void Reset();
}
