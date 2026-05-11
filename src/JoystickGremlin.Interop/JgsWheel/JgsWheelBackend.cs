// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Devices.Backends;

namespace JoystickGremlin.Interop.JgsWheel;

/// <summary>
/// <see cref="IVirtualDeviceBackend"/> wrapper around <see cref="JgsWheelDeviceManager"/>.
/// Exposes the JGS Wheel driver as a selectable backend in the registry alongside vJoy.
/// </summary>
/// <remarks>
/// The backend status reflects the prerequisite probe performed by
/// <see cref="JgsWheelPrerequisiteChecker"/>. When the driver is not installed the
/// backend is still registered (so the UI can list it and prompt the user to install
/// the driver) but reports <see cref="BackendStatus.NotInstalled"/>.
/// </remarks>
public sealed class JgsWheelBackend : IVirtualDeviceBackend
{
    /// <summary>Stable id used by profiles to pin this backend.</summary>
    public const string BackendId = "jgs-wheel";

    /// <inheritdoc />
    public string Id => BackendId;

    /// <inheritdoc />
    public string DisplayName => "JGS Wheel";

    /// <inheritdoc />
    public BackendKind Kind => BackendKind.RacingWheel;

    /// <inheritdoc />
    public BackendCapabilities Capabilities { get; } = new(
        MaxDevices: 1,
        MaxAxes: 4,
        MaxButtons: 32,
        MaxHats: 1,
        SupportsForceFeedback: true,
        SupportsIdentitySpoofing: true);

    /// <inheritdoc />
    public IVirtualDeviceManager Manager { get; }

    /// <summary>
    /// Initialises the backend with the shared <see cref="JgsWheelDeviceManager"/> instance
    /// from the DI container.
    /// </summary>
    public JgsWheelBackend(JgsWheelDeviceManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        Manager = manager;
    }

    /// <inheritdoc />
    public BackendStatus Status
    {
        get
        {
            var probe = JgsWheelPrerequisiteChecker.Check();
            if (!probe.IsInstalled)
                return BackendStatus.NotInstalled;
            if (probe.InterfaceDllPath is null)
                return BackendStatus.Incompatible;
            try
            {
                return Manager.IsAvailable ? BackendStatus.Ready : BackendStatus.NeedsTestSigning;
            }
            catch
            {
                return BackendStatus.Error;
            }
        }
    }
}
