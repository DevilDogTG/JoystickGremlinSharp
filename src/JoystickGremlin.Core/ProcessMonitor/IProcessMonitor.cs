// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Monitors the foreground window and raises <see cref="ForegroundProcessChanged"/>
/// whenever the active process changes.
/// </summary>
public interface IProcessMonitor : IDisposable
{
    /// <summary>
    /// Starts polling the foreground window for process changes.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops polling and releases any background resources.
    /// </summary>
    void Stop();

    /// <summary>
    /// Raised when the foreground window changes to a different process.
    /// The event argument is the full executable path (normalized, forward slashes),
    /// or an empty string if the path cannot be determined.
    /// </summary>
    event EventHandler<string>? ForegroundProcessChanged;
}
