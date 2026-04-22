// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Manages the lifecycle of vJoy virtual joystick devices.
/// Handles acquisition, release, and querying of available devices.
/// </summary>
public interface IVirtualDeviceManager : IDisposable
{
    /// <summary>Gets whether the vJoy driver is installed and running.</summary>
    bool IsAvailable { get; }

    /// <summary>Returns the IDs of all virtual devices that exist in the driver configuration.</summary>
    IReadOnlyList<uint> GetAvailableDeviceIds();

    /// <summary>
    /// Acquires the specified vJoy device for exclusive use by this process.
    /// </summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    /// <returns>The acquired virtual device.</returns>
    /// <exception cref="Core.Exceptions.VJoyException">
    /// Thrown if vJoy is unavailable, the device is busy, or acquisition fails.
    /// </exception>
    IVirtualDevice AcquireDevice(uint vjoyId);

    /// <summary>Releases the specified vJoy device, making it available to other processes.</summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    void ReleaseDevice(uint vjoyId);

    /// <summary>Releases all currently acquired vJoy devices.</summary>
    void ReleaseAll();
}
