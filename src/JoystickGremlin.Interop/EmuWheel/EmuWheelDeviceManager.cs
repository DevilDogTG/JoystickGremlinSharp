// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.EmuWheel;
using JoystickGremlin.Core.Exceptions;
using JoystickGremlin.Interop.Dill;
using JoystickGremlin.Interop.VJoy;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.EmuWheel;

/// <summary>
/// Implements <see cref="IEmuWheelDeviceManager"/> using a vJoy device slot with an
/// optional USB identity (VID/PID) registry spoof so that games detect the device as a
/// supported steering wheel.
/// </summary>
/// <remarks>
/// Device I/O (axis/button/hat) is handled by the underlying <see cref="IVirtualDeviceManager"/>.
/// Identity spoofing is handled by <see cref="VJoyRegistrySpoof"/> (writes registry) and
/// <see cref="VJoyDeviceReenumerator"/> (forces the vJoy bus to re-present child devices with
/// the updated hardware ID, making the change effective immediately without a reboot).
/// </remarks>
public sealed class EmuWheelDeviceManager : IEmuWheelDeviceManager
{
    private readonly IVirtualDeviceManager _vjoyManager;
    private readonly IDeviceManager _deviceManager;
    private readonly VJoyRegistrySpoof _spoof;
    private readonly VJoyDeviceReenumerator _reenumerator;
    private readonly ILogger<EmuWheelDeviceManager> _logger;

    private readonly object _deviceLock = new();
    private readonly object _spoofLock = new();
    private readonly Dictionary<uint, EmuWheelDevice> _acquiredDevices = [];

    private WheelModel? _activeModel;
    private bool _reEnumerationFailed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="EmuWheelDeviceManager"/>.
    /// </summary>
    /// <param name="vjoyManager">The vJoy virtual device manager for device I/O.</param>
    /// <param name="deviceManager">The physical device manager, used to register EmuWheel VID/PID filters.</param>
    /// <param name="logger">Logger instance.</param>
    public EmuWheelDeviceManager(
        IVirtualDeviceManager vjoyManager,
        IDeviceManager deviceManager,
        ILogger<EmuWheelDeviceManager> logger)
    {
        _vjoyManager    = vjoyManager;
        _deviceManager  = deviceManager;
        _logger         = logger;
        _spoof          = new VJoyRegistrySpoof(logger);
        _reenumerator   = new VJoyDeviceReenumerator(logger);
    }

    /// <inheritdoc/>
    public bool IsAvailable => _vjoyManager.IsAvailable;

    /// <inheritdoc/>
    public bool IsSpoofActive => _spoof.IsActive;

    /// <inheritdoc/>
    public bool RebootRecommended => _reEnumerationFailed;

    /// <inheritdoc/>
    public WheelModel? ActiveModel
    {
        get { lock (_spoofLock) return _activeModel; }
    }

    /// <inheritdoc/>
    public async Task ApplySpoofAsync(
        WheelModel model,
        uint vjoyId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var info = WheelModelRegistry.Get(model);

        // Register the EmuWheel VID/PID with DillDeviceManager so re-enumerated devices
        // with this identity are treated as virtual (excluded from the physical input list).
        if (_deviceManager is DillDeviceManager dill)
            dill.RegisterEmuWheelVidPid(info.VendorId, info.ProductId);

        var applied = await _spoof.ApplyAsync(model, vjoyId, cancellationToken).ConfigureAwait(false);
        lock (_spoofLock)
            _activeModel = model;

        if (!applied)
        {
            _reEnumerationFailed = true;
            _logger.LogWarning(
                "EmuWheel spoof could not be applied (registry access denied or key missing). " +
                "The vJoy device will be used with its default identity (VID 0x1234 / PID 0xBEAD). " +
                "Games that require a wheel VID/PID whitelist match may not detect it as a wheel.");
            return;
        }

        // Force the vJoy bus to re-enumerate its child HID devices so the updated VID/PID
        // takes effect immediately without requiring a reboot. RebootRecommended is set to
        // true only when this step fails (e.g. not running as administrator).
        var reEnumerated = await _reenumerator
            .ReEnumerateVJoyBusAsync(cancellationToken)
            .ConfigureAwait(false);

        _reEnumerationFailed = !reEnumerated;

        if (_reEnumerationFailed)
        {
            _logger.LogWarning(
                "EmuWheel: device re-enumeration failed — a system reboot is required for " +
                "games to detect the virtual device as a steering wheel.");
        }
        else
        {
            _logger.LogInformation(
                "EmuWheel: device re-enumerated successfully. " +
                "The vJoy device should now present as {Model} (VID 0x{VID:X4} / PID 0x{PID:X4}) to games.",
                model, info.VendorId, info.ProductId);
        }
    }

    /// <inheritdoc/>
    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        // Unregister the EmuWheel VID/PID filter before restoring so the original wheel
        // (if connected) becomes visible again in the physical device list.
        WheelModel? modelSnapshot;
        lock (_spoofLock)
            modelSnapshot = _activeModel;

        if (modelSnapshot.HasValue && _deviceManager is DillDeviceManager dill)
        {
            var info = WheelModelRegistry.Get(modelSnapshot.Value);
            dill.UnregisterEmuWheelVidPid(info.VendorId, info.ProductId);
        }

        ReleaseAll();
        await _spoof.RestoreAsync(cancellationToken).ConfigureAwait(false);
        lock (_spoofLock)
            _activeModel = null;
    }

    /// <inheritdoc/>
    public IEmuWheelDevice AcquireDevice(uint vjoyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsAvailable)
            throw new EmuWheelException("EmuWheel backend is unavailable: vJoy is not installed or not running.");

        lock (_deviceLock)
        {
            if (_acquiredDevices.TryGetValue(vjoyId, out var existing))
                return existing;

            var inner = _vjoyManager.AcquireDevice(vjoyId);
            WheelModel wheelModel;
            lock (_spoofLock)
                wheelModel = _activeModel ?? WheelModel.LogitechG29;
            var device = new EmuWheelDevice(inner, wheelModel);
            _acquiredDevices[vjoyId] = device;

            _logger.LogInformation(
                "EmuWheelDevice acquired: slot={VJoyId}, model={Model}",
                vjoyId, wheelModel);

            return device;
        }
    }

    /// <inheritdoc/>
    public void ReleaseDevice(uint vjoyId)
    {
        lock (_deviceLock)
        {
            if (!_acquiredDevices.Remove(vjoyId))
                return;
        }

        _vjoyManager.ReleaseDevice(vjoyId);
        _logger.LogInformation("EmuWheelDevice released: slot={VJoyId}", vjoyId);
    }

    /// <inheritdoc/>
    public void ReleaseAll()
    {
        uint[] ids;
        lock (_deviceLock)
        {
            ids = [.. _acquiredDevices.Keys];
        }

        foreach (var id in ids)
            ReleaseDevice(id);
    }

    /// <inheritdoc/>
    public IEmuWheelDevice GetDevice(uint vjoyId)
    {
        lock (_deviceLock)
        {
            if (_acquiredDevices.TryGetValue(vjoyId, out var device))
                return device;
        }

        throw new EmuWheelException(
            $"EmuWheel device slot {vjoyId} has not been acquired. " +
            "Call AcquireDevice() before GetDevice().");
    }

    /// <inheritdoc/>
    public Task RecoverIfNeededAsync(CancellationToken cancellationToken = default) =>
        _spoof.RecoverIfNeededAsync(cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ReleaseAll();

        // On clean exit, VID/PID are intentionally left in the registry so the vJoy driver
        // picks them up on the next reboot.  Only clear the sentinel file so the next startup
        // does not mistakenly trigger crash-recovery restore.
        _spoof.ClearSentinelOnExit();
    }
}
