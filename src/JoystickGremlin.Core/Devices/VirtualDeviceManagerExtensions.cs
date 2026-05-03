// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Exceptions;

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Provides helpers for working with <see cref="IVirtualDeviceManager"/>.
/// </summary>
public static class VirtualDeviceManagerExtensions
{
    /// <summary>
    /// Returns the requested vJoy device, acquiring it on demand when it has not yet been acquired
    /// by the current process. Thread-safe: concurrent calls for the same device ID are serialized
    /// inside <see cref="IVirtualDeviceManager.AcquireDevice"/>, which is idempotent.
    /// </summary>
    /// <param name="manager">The virtual device manager.</param>
    /// <param name="vjoyId">The 1-based vJoy device identifier.</param>
    /// <returns>The acquired virtual device.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager"/> is <see langword="null"/>.</exception>
    /// <exception cref="VJoyException">
    /// Thrown if the device cannot be acquired because vJoy is unavailable or the device is busy.
    /// </exception>
    public static IVirtualDevice GetOrAcquireDevice(this IVirtualDeviceManager manager, uint vjoyId)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return manager.AcquireDevice(vjoyId);
    }

    /// <summary>
    /// Forces re-acquisition of a vJoy device by releasing it (if held) and acquiring it fresh.
    /// Use this to recover from lost device ownership — for example when another application
    /// temporarily took exclusive control of the device.
    /// </summary>
    /// <param name="manager">The virtual device manager.</param>
    /// <param name="vjoyId">The 1-based vJoy device identifier.</param>
    /// <returns>The freshly acquired virtual device.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager"/> is <see langword="null"/>.</exception>
    /// <exception cref="VJoyException">
    /// Thrown if the device cannot be re-acquired because vJoy is unavailable or the device is busy.
    /// </exception>
    public static IVirtualDevice ForceReacquireDevice(this IVirtualDeviceManager manager, uint vjoyId)
    {
        ArgumentNullException.ThrowIfNull(manager);

        manager.ReleaseDevice(vjoyId);
        return manager.AcquireDevice(vjoyId);
    }
}
