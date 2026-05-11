// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.JgsWheel;

/// <summary>
/// <see cref="IVirtualDeviceManager"/> implementation backed by the JGS Wheel driver
/// (<c>jgswheel.sys</c> + <c>JgsWheelInterface.dll</c>).
/// </summary>
/// <remarks>
/// <para>
/// The JGS Wheel driver is a fork of BrunnerInnovation/vJoy that exposes per-device
/// VID/PID and a Logitech-G29-compatible HID report descriptor. The user-mode interface
/// DLL preserves the vJoy ABI so once the driver is built and installed, this manager
/// can use the same P/Invoke surface as <c>VJoyDeviceManager</c>.
/// </para>
/// <para>
/// Until the driver binary is built, this manager is a graceful stub: it reports
/// <see cref="IsAvailable"/> = <c>false</c>, returns an empty device list, and throws
/// <see cref="VJoyException"/> from acquire / release operations with a clear message
/// pointing the user to <c>installer/wheel-driver/README.md</c>. The presence of the
/// driver service is probed via <see cref="JgsWheelPrerequisiteChecker"/>.
/// </para>
/// </remarks>
public sealed class JgsWheelDeviceManager : IVirtualDeviceManager
{
    private readonly ILogger<JgsWheelDeviceManager> _logger;
    private readonly JgsWheelPrerequisiteResult _prereq;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="JgsWheelDeviceManager"/>.</summary>
    public JgsWheelDeviceManager(ILogger<JgsWheelDeviceManager> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _prereq = JgsWheelPrerequisiteChecker.Check();
        if (_prereq.IsInstalled)
        {
            _logger.LogInformation(
                "JgsWheelDeviceManager initialised; driver image={ImagePath}, interfaceDll={InterfaceDll}",
                _prereq.ImagePath ?? "(unknown)",
                _prereq.InterfaceDllPath ?? "(missing)");
        }
        else
        {
            _logger.LogInformation(
                "JgsWheelDeviceManager initialised but driver service '{ServiceName}' not found — backend will report NotInstalled.",
                JgsWheelPrerequisiteChecker.ServiceName);
        }
    }

    /// <inheritdoc/>
    public bool IsAvailable => _prereq.IsOk;

    /// <inheritdoc/>
    public IReadOnlyList<uint> GetAvailableDeviceIds() => Array.Empty<uint>();

    /// <inheritdoc/>
    public VirtualDeviceCapabilities GetCapabilities(uint vjoyId) =>
        new VirtualDeviceCapabilities(AxisCount: 0, ButtonCount: 0, HatCount: 0);

    /// <inheritdoc/>
    public VirtualDeviceStatus GetStatus(uint vjoyId) =>
        _prereq.IsOk ? VirtualDeviceStatus.Free : VirtualDeviceStatus.Missing;

    /// <inheritdoc/>
    public string? GetConfigurationToolPath() => null;

    /// <inheritdoc/>
    public IVirtualDevice AcquireDevice(uint vjoyId) =>
        throw new VJoyException(BuildNotInstalledMessage("acquire"));

    /// <inheritdoc/>
    public void ReleaseDevice(uint vjoyId)
    {
        // No-op — nothing was ever acquired. Logged at debug only to avoid noise on shutdown.
        _logger.LogDebug("ReleaseDevice({VjoyId}) called on JgsWheelDeviceManager — no-op (driver not yet built).", vjoyId);
    }

    /// <inheritdoc/>
    public void ReleaseAll()
    {
        _logger.LogDebug("ReleaseAll called on JgsWheelDeviceManager — no-op (driver not yet built).");
    }

    /// <inheritdoc/>
    public IVirtualDevice GetDevice(uint vjoyId) =>
        throw new VJoyException(BuildNotInstalledMessage("query"));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseAll();
    }

    private static string BuildNotInstalledMessage(string operation) =>
        $"Cannot {operation} JGS Wheel device: the wheel driver has not been built or installed yet. " +
        "Build instructions are in installer/wheel-driver/README.md. The vJoy backend remains the default.";
}
