// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Describes the install state of HidHide on the local machine.
/// </summary>
/// <param name="IsInstalled">Whether the HidHide CLI executable can be located.</param>
/// <param name="CliPath">Resolved CLI path, or <c>null</c> when not installed.</param>
/// <param name="Version">CLI version string (when retrievable), else <c>null</c>.</param>
public sealed record HidHideStatus(bool IsInstalled, string? CliPath, string? Version)
{
    /// <summary>Convenience factory for the not-installed result.</summary>
    public static HidHideStatus NotInstalled() => new(false, null, null);
}

/// <summary>
/// A single instance/HID-class device known to HidHide.
/// Identified by symbolic link / instance path passed to <c>--dev-hide</c>/<c>--dev-unhide</c>.
/// </summary>
/// <param name="InstancePath">Symbolic link / device instance path used by HidHide CLI.</param>
/// <param name="DisplayName">Friendly name shown in the UI.</param>
/// <param name="IsHidden">Whether the device is currently in the hidden list.</param>
public sealed record HidHideDeviceEntry(string InstancePath, string DisplayName, bool IsHidden);

/// <summary>
/// Whitelisted application that may see hidden HidHide devices.
/// </summary>
/// <param name="ImagePath">Absolute path to the executable.</param>
public sealed record HidHideAppWhitelistEntry(string ImagePath);

/// <summary>
/// User-mode façade over the HidHide CLI (and, when present, the COM API).
/// All operations are async because the CLI shells out to <c>HidHideCLI.exe</c>.
/// </summary>
/// <remarks>
/// HidHide must be installed separately by the user — see
/// <see href="https://github.com/nefarius/HidHide/releases"/>. The default install path
/// is probed automatically; an explicit path can be supplied via
/// <see cref="Configuration.AppSettings.HidHideCliPath"/>.
/// </remarks>
public interface IHidHideService
{
    /// <summary>Probes the local install and returns the current status.</summary>
    HidHideStatus GetStatus();

    /// <summary>Lists all devices known to HidHide along with their hidden state.</summary>
    Task<IReadOnlyList<HidHideDeviceEntry>> ListDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>Hides the device with the given instance path from non-whitelisted apps.</summary>
    Task HideDeviceAsync(string instancePath, CancellationToken cancellationToken = default);

    /// <summary>Restores visibility of the device with the given instance path.</summary>
    Task UnhideDeviceAsync(string instancePath, CancellationToken cancellationToken = default);

    /// <summary>Lists applications currently in the whitelist.</summary>
    Task<IReadOnlyList<HidHideAppWhitelistEntry>> ListWhitelistAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds an application to the whitelist so it can see hidden devices.</summary>
    Task AddWhitelistEntryAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>Removes an application from the whitelist.</summary>
    Task RemoveWhitelistEntryAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>Toggles the global HidHide cloak (master enable/disable).</summary>
    Task SetCloakEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
