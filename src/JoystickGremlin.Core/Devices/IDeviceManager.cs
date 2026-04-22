// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Events;

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Manages enumeration of physical input devices and dispatches raw input events.
/// Implementations wrap platform-specific device APIs (e.g. DILL on Windows).
/// </summary>
public interface IDeviceManager : IDisposable
{
    /// <summary>Gets the list of currently connected physical devices.</summary>
    IReadOnlyList<IPhysicalDevice> Devices { get; }

    /// <summary>Raised when a new device is connected.</summary>
    event EventHandler<IPhysicalDevice>? DeviceConnected;

    /// <summary>Raised when a device is disconnected.</summary>
    event EventHandler<IPhysicalDevice>? DeviceDisconnected;

    /// <summary>Raised for every raw input state change from any device.</summary>
    event EventHandler<InputEvent>? InputReceived;

    /// <summary>
    /// Initialises the underlying device API and begins capturing events.
    /// Must be called once before any events are raised or devices are listed.
    /// </summary>
    void Initialize();
}
