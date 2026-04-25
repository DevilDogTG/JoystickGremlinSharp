// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// No-op implementation of <see cref="IProcessMonitor"/> used in tests and non-Windows builds.
/// Never fires <see cref="ForegroundProcessChanged"/>.
/// </summary>
public sealed class NullProcessMonitor : IProcessMonitor
{
    /// <inheritdoc/>
    public event EventHandler<string>? ForegroundProcessChanged;

    /// <inheritdoc/>
    public void Start() { }

    /// <inheritdoc/>
    public void Stop() { }

    /// <inheritdoc/>
    public void Dispose() { }
}
