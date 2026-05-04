// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.EmuWheel;

/// <summary>
/// Represents a virtual wheel device managed by the EmuWheel backend.
/// The EmuWheel backend presents this device to the OS using a spoofed wheel VID/PID
/// so that games detect it as a steering wheel rather than a generic joystick.
/// </summary>
public interface IEmuWheelDevice
{
    /// <summary>Gets the underlying vJoy device ID used by this EmuWheel device (1-based).</summary>
    uint DeviceId { get; }

    /// <summary>Gets the wheel model this device is currently presenting as.</summary>
    WheelModel WheelModel { get; }

    /// <summary>Gets the number of configured axes.</summary>
    int AxisCount { get; }

    /// <summary>Gets the number of configured buttons.</summary>
    int ButtonCount { get; }

    /// <summary>Gets the number of configured hats/POVs.</summary>
    int HatCount { get; }

    /// <summary>Sets an axis value in the range [-1.0, 1.0].</summary>
    void SetAxis(int axisIndex, double value);

    /// <summary>Sets a button state.</summary>
    void SetButton(int buttonIndex, bool pressed);

    /// <summary>Sets a hat/POV direction in degrees (0–35999), or -1 for center.</summary>
    void SetHat(int hatIndex, int degrees);

    /// <summary>Gets the last axis value written by this process, or <c>null</c> when unavailable.</summary>
    double? GetAxis(int axisIndex);

    /// <summary>Gets the last button state written by this process, or <c>null</c> when unavailable.</summary>
    bool? GetButton(int buttonIndex);

    /// <summary>Gets the last hat value written by this process, or <c>null</c> when unavailable.</summary>
    int? GetHat(int hatIndex);

    /// <summary>Resets all inputs on the device to their neutral state.</summary>
    void Reset();
}
