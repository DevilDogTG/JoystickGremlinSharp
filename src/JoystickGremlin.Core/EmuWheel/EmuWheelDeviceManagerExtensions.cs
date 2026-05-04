// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Exceptions;

namespace JoystickGremlin.Core.EmuWheel;

/// <summary>
/// Provides helpers for working with <see cref="IEmuWheelDeviceManager"/>.
/// </summary>
public static class EmuWheelDeviceManagerExtensions
{
    /// <summary>
    /// Returns the requested EmuWheel device, acquiring it on demand when it has not yet been acquired
    /// by the current process. Thread-safe: concurrent calls for the same device ID are serialized
    /// inside <see cref="IEmuWheelDeviceManager.AcquireDevice"/>, which is idempotent.
    /// </summary>
    /// <param name="manager">The EmuWheel device manager.</param>
    /// <param name="vjoyId">The 1-based vJoy device identifier.</param>
    /// <returns>The acquired EmuWheel device.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager"/> is <see langword="null"/>.</exception>
    /// <exception cref="EmuWheelException">Thrown if the device cannot be acquired.</exception>
    public static IEmuWheelDevice GetOrAcquireDevice(this IEmuWheelDeviceManager manager, uint vjoyId)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return manager.AcquireDevice(vjoyId);
    }

    /// <summary>
    /// Forces re-acquisition of an EmuWheel device by releasing it (if held) and acquiring it fresh.
    /// Use this to recover from lost device ownership.
    /// </summary>
    /// <param name="manager">The EmuWheel device manager.</param>
    /// <param name="vjoyId">The 1-based vJoy device identifier.</param>
    /// <returns>The freshly acquired EmuWheel device.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager"/> is <see langword="null"/>.</exception>
    /// <exception cref="EmuWheelException">Thrown if the device cannot be re-acquired.</exception>
    public static IEmuWheelDevice ForceReacquireDevice(this IEmuWheelDeviceManager manager, uint vjoyId)
    {
        ArgumentNullException.ThrowIfNull(manager);
        manager.ReleaseDevice(vjoyId);
        return manager.AcquireDevice(vjoyId);
    }
}
