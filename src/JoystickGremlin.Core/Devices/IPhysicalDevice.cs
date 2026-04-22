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
}
