// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Startup;

/// <summary>
/// Manages the application's "start with Windows" registration.
/// </summary>
public interface IStartupService
{
    /// <summary>Gets a value indicating whether the application is currently registered to start with Windows.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Registers the application to launch automatically when the current user logs in.
    /// </summary>
    /// <param name="executablePath">Absolute path to the application executable.</param>
    void Enable(string executablePath);

    /// <summary>
    /// Removes the application's automatic startup registration, if present.
    /// </summary>
    void Disable();
}
