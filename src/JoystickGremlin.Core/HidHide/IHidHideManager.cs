// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Orchestrates the HidHide integration: applies/reverts device hiding in response to
/// event-pipeline start/stop transitions and exposes the current integration status.
/// </summary>
public interface IHidHideManager : IDisposable
{
    /// <summary>
    /// Performs crash-recovery cleanup on startup: removes any device instance IDs from the
    /// HidHide block list that were added by a previous session (which may have crashed without
    /// reverting). Should be called once during application startup, before any other operation.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the current status of the HidHide integration.</summary>
    HidHideStatus Status { get; }

    /// <summary>
    /// Gets the error message from the last failed operation, or <see langword="null"/> if no error has occurred.
    /// Updated whenever <see cref="Status"/> transitions to <see cref="HidHideStatus.Error"/>.
    /// </summary>
    string? LastError { get; }

    /// <summary>Gets a value indicating whether hiding is currently applied.</summary>
    bool IsApplied { get; }

    /// <summary>Raised whenever <see cref="Status"/> changes.</summary>
    event EventHandler? StatusChanged;

    /// <summary>
    /// Applies hiding for all devices listed in <see cref="Configuration.AppSettings.HiddenDeviceInstanceIds"/>
    /// and whitelists this application's executable.
    /// Safe to call when already applied (idempotent).
    /// </summary>
    Task ApplyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all instance IDs added by this session from the HidHide block list and
    /// optionally removes this application from the bypass list.
    /// Safe to call when not applied (idempotent).
    /// </summary>
    Task RevertAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-reads driver state and re-evaluates the integration status.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
