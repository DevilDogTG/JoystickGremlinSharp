// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Exceptions;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Implements <see cref="IVirtualDeviceManager"/> using the vJoyInterface native DLL.
/// Manages acquisition and release of vJoy virtual joystick devices for this process.
/// </summary>
/// <remarks>
/// vJoy devices must be explicitly acquired before use and released when no longer needed.
/// This manager tracks all acquired devices and releases them on disposal.
/// </remarks>
public sealed class VJoyDeviceManager : IVirtualDeviceManager
{
    /// <summary>Minimum supported vJoy driver version (2.1.8).</summary>
    private const short MinVJoyVersion = 0x218;

    /// <summary>Maximum number of vJoy device slots supported by the driver.</summary>
    private const uint MaxDeviceSlots = 16;

    private readonly Dictionary<uint, VJoyDevice> _acquiredDevices = [];
    private bool _disposed;

    /// <inheritdoc/>
    public bool IsAvailable => VJoyNative.vJoyEnabled();

    /// <inheritdoc/>
    public IReadOnlyList<uint> GetAvailableDeviceIds()
    {
        var ids = new List<uint>();
        for (uint i = 1; i <= MaxDeviceSlots; i++)
        {
            var status = (VjdStatus)VJoyNative.GetVJDStatus(i);
            if (status != VjdStatus.Missing && status != VjdStatus.Unknown)
                ids.Add(i);
        }
        return ids;
    }

    /// <inheritdoc/>
    /// <exception cref="VJoyException">
    /// Thrown if vJoy is unavailable, the version is too old, the device is not free,
    /// the device is already held by this process, or the acquisition call fails.
    /// </exception>
    public IVirtualDevice AcquireDevice(uint vjoyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!VJoyNative.vJoyEnabled())
            throw new VJoyException("vJoy is not currently running.");

        short version = VJoyNative.GetvJoyVersion();
        if (version < MinVJoyVersion)
            throw new VJoyException(
                $"Incompatible vJoy version 0x{version:X}. Version 0x{MinVJoyVersion:X} or higher required.");

        if (_acquiredDevices.ContainsKey(vjoyId))
            throw new VJoyException($"vJoy device {vjoyId} is already acquired by this process.");

        var status = (VjdStatus)VJoyNative.GetVJDStatus(vjoyId);
        if (status != VjdStatus.Free)
            throw new VJoyException(
                $"vJoy device {vjoyId} is not available (status: {status}). " +
                $"It may be in use by another application (PID {VJoyNative.GetOwnerPid(vjoyId)}).");

        if (!VJoyNative.AcquireVJD(vjoyId))
            throw new VJoyException($"Failed to acquire vJoy device {vjoyId}.");

        var device = new VJoyDevice(vjoyId);
        device.Reset();
        _acquiredDevices[vjoyId] = device;
        return device;
    }

    /// <inheritdoc/>
    public void ReleaseDevice(uint vjoyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_acquiredDevices.Remove(vjoyId))
            return;

        VJoyNative.RelinquishVJD(vjoyId);
    }

    /// <inheritdoc/>
    public void ReleaseAll()
    {
        foreach (uint vjoyId in _acquiredDevices.Keys.ToList())
        {
            VJoyNative.RelinquishVJD(vjoyId);
        }
        _acquiredDevices.Clear();
    }

    /// <inheritdoc/>
    public IVirtualDevice GetDevice(uint vjoyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_acquiredDevices.TryGetValue(vjoyId, out var device))
            return device;

        throw new VJoyException($"vJoy device {vjoyId} has not been acquired. Call AcquireDevice first.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ReleaseAll();
    }
}
