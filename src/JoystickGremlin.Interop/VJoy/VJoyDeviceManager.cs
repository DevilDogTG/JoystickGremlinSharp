// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

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
    /// <summary>Minimum supported vJoy driver version (BrunnerInnovation fork v2.2.2).</summary>
    private const short MinVJoyVersion = 0x222;

    /// <summary>Maximum number of vJoy device slots supported by the driver.</summary>
    private const uint MaxDeviceSlots = 16;

    private readonly Dictionary<uint, VJoyDevice> _acquiredDevices = [];
    private readonly ILogger<VJoyDeviceManager> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="VJoyDeviceManager"/>.
    /// </summary>
    public VJoyDeviceManager(ILogger<VJoyDeviceManager> logger)
    {
        // Ensure the installed vJoy DLL is loaded before any P/Invoke call.
        VJoyNativeLibraryLoader.EnsureLoaded();
        _logger = logger;
        _logger.LogInformation(
            "VJoyDeviceManager initialised; vJoyInterface.dll loaded from {DllPath}",
            VJoyNativeLibraryLoader.LoadedDllPath ?? "(default search path — bundled or PATH)");
    }

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

        _logger.LogInformation("Available vJoy device IDs: {DeviceIds}", ids.Count == 0 ? "(none)" : string.Join(", ", ids));
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

        _logger.LogInformation("Attempting to acquire vJoy device {VJoyId}", vjoyId);

        if (!VJoyNative.vJoyEnabled())
        {
            _logger.LogWarning("Cannot acquire vJoy device {VJoyId} because vJoy is not running", vjoyId);
            throw new VJoyException("vJoy is not currently running.");
        }

        short version = VJoyNative.GetvJoyVersion();
        if (version < MinVJoyVersion)
        {
            _logger.LogWarning(
                "Cannot acquire vJoy device {VJoyId} because vJoy version 0x{Version:X} is older than required 0x{MinimumVersion:X}",
                vjoyId,
                version,
                MinVJoyVersion);
            throw new VJoyException(
                $"Incompatible vJoy version 0x{version:X}. Version 0x{MinVJoyVersion:X} or higher required.");
        }

        if (_acquiredDevices.ContainsKey(vjoyId))
        {
            _logger.LogDebug("vJoy device {VJoyId} is already acquired by this process", vjoyId);
            throw new VJoyException($"vJoy device {vjoyId} is already acquired by this process.");
        }

        var status = (VjdStatus)VJoyNative.GetVJDStatus(vjoyId);
        _logger.LogDebug("vJoy device {VJoyId} status before acquire: {Status}", vjoyId, status);
        if (status != VjdStatus.Free)
        {
            var ownerPid = VJoyNative.GetOwnerPid(vjoyId);
            _logger.LogWarning(
                "vJoy device {VJoyId} is not free; status {Status}, owner PID {OwnerPid}",
                vjoyId,
                status,
                ownerPid);
            throw new VJoyException(
                $"vJoy device {vjoyId} is not available (status: {status}). " +
                $"It may be in use by another application (PID {ownerPid}).");
        }

        if (!VJoyNative.AcquireVJD(vjoyId))
        {
            _logger.LogWarning("vJoy driver rejected acquire request for device {VJoyId}", vjoyId);
            throw new VJoyException($"Failed to acquire vJoy device {vjoyId}.");
        }

        var device = new VJoyDevice(vjoyId);
        device.Reset();
        _acquiredDevices[vjoyId] = device;
        _logger.LogInformation(
            "Acquired vJoy device {VJoyId} with {AxisCount} axes, {ButtonCount} buttons, {HatCount} hats",
            vjoyId,
            device.AxisCount,
            device.ButtonCount,
            device.HatCount);
        return device;
    }

    /// <inheritdoc/>
    public void ReleaseDevice(uint vjoyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_acquiredDevices.Remove(vjoyId))
            return;

        VJoyNative.RelinquishVJD(vjoyId);
        _logger.LogInformation("Released vJoy device {VJoyId}", vjoyId);
    }

    /// <inheritdoc/>
    public void ReleaseAll()
    {
        foreach (uint vjoyId in _acquiredDevices.Keys.ToList())
        {
            VJoyNative.RelinquishVJD(vjoyId);
            _logger.LogInformation("Released vJoy device {VJoyId}", vjoyId);
        }
        _acquiredDevices.Clear();
    }

    /// <inheritdoc/>
    public IVirtualDevice GetDevice(uint vjoyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_acquiredDevices.TryGetValue(vjoyId, out var device))
        {
            _logger.LogDebug("Using previously acquired vJoy device {VJoyId}", vjoyId);
            return device;
        }

        _logger.LogDebug("vJoy device {VJoyId} has not been acquired yet", vjoyId);
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
