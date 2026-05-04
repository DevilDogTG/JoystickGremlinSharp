// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Represents the current status of a virtual device slot.
/// </summary>
public enum VirtualDeviceStatus
{
    /// <summary>The device is owned by the current process.</summary>
    Owned,

    /// <summary>The device is free and can be acquired.</summary>
    Free,

    /// <summary>The device is owned by another process.</summary>
    Busy,

    /// <summary>The device is not configured or not present.</summary>
    Missing,

    /// <summary>The driver returned an unknown or unexpected status.</summary>
    Unknown,
}
