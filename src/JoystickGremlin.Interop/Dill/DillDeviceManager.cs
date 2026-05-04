// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.Dill;

/// <summary>
/// Implements <see cref="IDeviceManager"/> using the DILL (Direct Input Listener Library) native DLL.
/// Manages the DILL lifecycle, device enumeration, and event dispatching.
/// </summary>
/// <remarks>
/// The DILL library may only be initialised once per process. This class enforces that constraint
/// via a static flag. Callback delegates are stored as instance fields to prevent GC collection.
/// </remarks>
public sealed class DillDeviceManager : IDeviceManager
{
    // Guards against calling DILL init() more than once per process.
    private static bool _dillInitialized;
    private static readonly object _initLock = new();

    private readonly List<DillDevice> _devices = [];
    private readonly HashSet<Guid> _virtualDeviceGuids = [];
    private readonly ILogger<DillDeviceManager> _logger;

    // Stored to prevent the GC from collecting unmanaged delegates.
    private InputEventCallback? _inputEventCallback;
    private DeviceChangeCallback? _deviceChangeCallback;

    private bool _disposed;

    /// <inheritdoc/>
    public IReadOnlyList<IPhysicalDevice> Devices => _devices;

    /// <inheritdoc/>
    public event EventHandler<IPhysicalDevice>? DeviceConnected;

    /// <inheritdoc/>
    public event EventHandler<IPhysicalDevice>? DeviceDisconnected;

    /// <inheritdoc/>
    public event EventHandler<InputEvent>? InputReceived;

    /// <summary>
    /// Initializes a new instance of <see cref="DillDeviceManager"/>.
    /// </summary>
    public DillDeviceManager(ILogger<DillDeviceManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <exception cref="DeviceException">Thrown if DILL cannot be initialised.</exception>
    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Initializing DILL device manager");

        lock (_initLock)
        {
            if (!_dillInitialized)
            {
                DillNative.init();
                _dillInitialized = true;
                _logger.LogInformation("DILL native library initialized");
            }
        }

        RegisterCallbacks();
        RefreshDevices();
    }

    /// <summary>
    /// Reads the current axis value for a device input via a direct DILL poll.
    /// </summary>
    public int GetAxis(Guid deviceGuid, uint axisIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return DillNative.get_axis(DillGuidConverter.FromGuid(deviceGuid), axisIndex);
    }

    /// <summary>
    /// Reads the current button state for a device input via a direct DILL poll.
    /// </summary>
    public bool GetButton(Guid deviceGuid, uint buttonIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return DillNative.get_button(DillGuidConverter.FromGuid(deviceGuid), buttonIndex);
    }

    /// <summary>
    /// Reads the current hat value for a device input via a direct DILL poll.
    /// </summary>
    public int GetHat(Guid deviceGuid, uint hatIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return DillNative.get_hat(DillGuidConverter.FromGuid(deviceGuid), hatIndex);
    }

    /// <summary>
    /// Returns whether the device with the given GUID is currently connected.
    /// </summary>
    public bool DeviceExists(Guid deviceGuid)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return DillNative.device_exists(DillGuidConverter.FromGuid(deviceGuid));
    }

    private void RegisterCallbacks()
    {
        _inputEventCallback = OnNativeInputEvent;
        _deviceChangeCallback = OnNativeDeviceChange;

        DillNative.set_input_event_callback(_inputEventCallback);
        DillNative.set_device_change_callback(_deviceChangeCallback);
    }

    private void RefreshDevices()
    {
        _devices.Clear();
        _virtualDeviceGuids.Clear();
        uint count = DillNative.get_device_count();
        _logger.LogInformation("Refreshing physical devices from DILL; reported device count {DeviceCount}", count);
        for (uint i = 0; i < count; i++)
        {
            var summary = DillNative.get_device_information_by_index(i);
            var device = new DillDevice(summary);
            if (device.IsVirtual)
            {
                _virtualDeviceGuids.Add(device.Guid);
                _logger.LogDebug(
                    "Skipping virtual device {Name} ({Guid}) — vJoy output devices must not appear as physical inputs",
                    device.Name,
                    device.Guid);
                continue;
            }
            _devices.Add(device);
            _logger.LogInformation(
                "Detected device {Name} ({Guid}) axes {AxisCount} buttons {ButtonCount} hats {HatCount}",
                device.Name,
                device.Guid,
                device.AxisCount,
                device.ButtonCount,
                device.HatCount);
        }
    }

    private void OnNativeInputEvent(NativeJoystickInputData data)
    {
        if (_disposed)
            return;

        var deviceGuid = DillGuidConverter.ToGuid(data.DeviceGuid);
        if (_virtualDeviceGuids.Contains(deviceGuid))
        {
            _logger.LogTrace("Ignoring input event from virtual device {DeviceGuid}", deviceGuid);
            return;
        }

        InputType? inputType = data.InputType switch
        {
            1 => InputType.JoystickAxis,
            2 => InputType.JoystickButton,
            3 => InputType.JoystickHat,
            _ => null
        };

        if (inputType is null)
        {
            _logger.LogDebug("Ignoring DILL input event with unknown input type {InputType}", data.InputType);
            return;
        }

        // DILL reports InputIndex as a 1-based index for all input types (buttons, axes, hats),
        // matching the 1-based identifiers used throughout the rest of the system.
        int identifier = data.InputIndex;

        // Axis values arrive as raw DirectInput signed-short integers (range −32768 to 32767).
        // Normalise to [−1.0, 1.0] so that the pipeline and vJoy output receive consistent values.
        double value = inputType == InputType.JoystickAxis
            ? Math.Clamp(data.Value / 32767.0, -1.0, 1.0)
            : data.Value;

        var evt = new InputEvent(
            inputType.Value,
            DillGuidConverter.ToGuid(data.DeviceGuid),
            identifier,
            value);

        _logger.LogDebug(
            "DILL input event: device {DeviceGuid}, type {InputType}, identifier {Identifier}, raw value {RawValue}, normalized value {Value}",
            evt.DeviceGuid,
            evt.InputType,
            evt.Identifier,
            data.Value,
            evt.Value);

        if (InputReceived is null)
        {
            _logger.LogDebug("No subscribers are listening for DILL input events");
            return;
        }

        InputReceived.Invoke(this, evt);
    }

    private void OnNativeDeviceChange(NativeDeviceSummary data, byte actionType)
    {
        if (_disposed)
            return;

        var device = new DillDevice(data);

        if (device.IsVirtual)
        {
            if (actionType == 1)
                _virtualDeviceGuids.Add(device.Guid);
            else if (actionType == 2)
                _virtualDeviceGuids.Remove(device.Guid);
            return;
        }

        if (actionType == 1)
        {
            _devices.RemoveAll(d => d.Guid == device.Guid);
            _devices.Add(device);
            _logger.LogInformation("Device connected: {Name} ({Guid})", device.Name, device.Guid);
            DeviceConnected?.Invoke(this, device);
        }
        else if (actionType == 2)
        {
            _devices.RemoveAll(d => d.Guid == device.Guid);
            _logger.LogInformation("Device disconnected: {Name} ({Guid})", device.Name, device.Guid);
            DeviceDisconnected?.Invoke(this, device);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Clear callback references. DILL has no unregister API; setting null delegates
        // prevents any future invocations from reaching managed code after disposal.
        _inputEventCallback = null;
        _deviceChangeCallback = null;

        _devices.Clear();
        _virtualDeviceGuids.Clear();
    }
}
