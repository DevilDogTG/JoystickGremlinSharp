// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Devices.Backends;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Adapter that exposes the existing <see cref="VJoyDeviceManager"/> as an
/// <see cref="IVirtualDeviceBackend"/> so it can be registered alongside future
/// backends (JGS Wheel, ViGEm wheel target, etc.) in the <see cref="IBackendRegistry"/>.
/// </summary>
public sealed class VJoyBackend : IVirtualDeviceBackend
{
    /// <summary>Stable id used by profiles to pin this backend.</summary>
    public const string BackendId = "vjoy";

    /// <inheritdoc />
    public string Id => BackendId;

    /// <inheritdoc />
    public string DisplayName => "vJoy";

    /// <inheritdoc />
    public BackendKind Kind => BackendKind.GenericController;

    /// <inheritdoc />
    public BackendCapabilities Capabilities { get; } = new(
        MaxDevices: 16,
        MaxAxes: 8,
        MaxButtons: 128,
        MaxHats: 4,
        SupportsForceFeedback: true,
        SupportsIdentitySpoofing: false);

    /// <inheritdoc />
    public IVirtualDeviceManager Manager { get; }

    /// <summary>
    /// Initialises the backend with the shared <see cref="VJoyDeviceManager"/> instance
    /// from the DI container.
    /// </summary>
    public VJoyBackend(IVirtualDeviceManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        Manager = manager;
    }

    /// <inheritdoc />
    public BackendStatus Status
    {
        get
        {
            // Pure registry/file-system probe — does not require the native DLL to load.
            var probe = VJoyPrerequisiteChecker.Check();
            if (!probe.IsInstalled)
                return BackendStatus.NotInstalled;
            if (!probe.IsCompatible)
                return BackendStatus.Incompatible;

            // Driver service answering the "enabled" P/Invoke confirms the kernel side is live.
            try
            {
                return Manager.IsAvailable ? BackendStatus.Ready : BackendStatus.Error;
            }
            catch
            {
                return BackendStatus.Error;
            }
        }
    }
}
