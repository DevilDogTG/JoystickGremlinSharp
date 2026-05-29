// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Maintains this application's HidHide whitelist entry so games can see physical
/// devices that the user has hidden via the native HidHide configuration client.
/// Device hiding itself is delegated to the native HidHide UI — this manager does
/// not block or unblock device instance IDs.
/// </summary>
public interface IHidHideManager : IDisposable
{
    /// <summary>
    /// Runs on application startup: whitelists this executable's path so the app
    /// can see hidden devices. Called once from <c>App.axaml.cs</c>.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
