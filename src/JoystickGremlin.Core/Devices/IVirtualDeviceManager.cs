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

    /// <summary>Gets the configured capabilities of the specified virtual device slot.</summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    VirtualDeviceCapabilities GetCapabilities(uint vjoyId);

    /// <summary>Gets the current status of the specified virtual device slot.</summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    VirtualDeviceStatus GetStatus(uint vjoyId);

    /// <summary>Gets the vJoy configuration tool path, or <c>null</c> when unavailable.</summary>
    string? GetConfigurationToolPath();

    /// <summary>
    /// Acquires the specified vJoy device for exclusive use by this process.
    /// Idempotent: if this process already holds the device, the existing instance is returned
    /// without resetting it. Thread-safe: concurrent calls for the same device ID are serialized.
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

    /// <summary>
    /// Returns a previously acquired device by ID.
    /// </summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    /// <returns>The acquired <see cref="IVirtualDevice"/>.</returns>
    /// <exception cref="Core.Exceptions.VJoyException">Thrown if the device has not been acquired.</exception>
    IVirtualDevice GetDevice(uint vjoyId);
}
