// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Represents a physical joystick or gamepad device detected by the DILL library.
/// </summary>
public interface IPhysicalDevice
{
    /// <summary>Gets the unique hardware GUID of the device.</summary>
    Guid Guid { get; }

    /// <summary>Gets the human-readable device name.</summary>
    string Name { get; }

    /// <summary>Gets the number of axes on the device.</summary>
    int AxisCount { get; }

    /// <summary>Gets the number of buttons on the device.</summary>
    int ButtonCount { get; }

    /// <summary>Gets the number of hat switches on the device.</summary>
    int HatCount { get; }

    /// <summary>
    /// Gets the Windows Device Instance ID (e.g.
    /// <c>HID\VID_054C&amp;PID_05C4\6&amp;1A2B3C4D&amp;0&amp;0000</c>), or <c>null</c>
    /// if it cannot be determined.
    /// Used to correlate this device with HidHide's block list.
    /// </summary>
    string? InstanceId { get; }
}
