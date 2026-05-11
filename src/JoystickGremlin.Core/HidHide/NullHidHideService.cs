// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Default no-op <see cref="IHidHideService"/> registered by Core. The Interop layer
/// overrides this with a real CLI-backed implementation on Windows.
/// </summary>
public sealed class NullHidHideService : IHidHideService
{
    /// <inheritdoc />
    public HidHideStatus GetStatus() => HidHideStatus.NotInstalled();

    /// <inheritdoc />
    public Task<IReadOnlyList<HidHideDeviceEntry>> ListDevicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HidHideDeviceEntry>>(Array.Empty<HidHideDeviceEntry>());

    /// <inheritdoc />
    public Task HideDeviceAsync(string instancePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task UnhideDeviceAsync(string instancePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyList<HidHideAppWhitelistEntry>> ListWhitelistAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HidHideAppWhitelistEntry>>(Array.Empty<HidHideAppWhitelistEntry>());

    /// <inheritdoc />
    public Task AddWhitelistEntryAsync(string imagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveWhitelistEntryAsync(string imagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetCloakEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
