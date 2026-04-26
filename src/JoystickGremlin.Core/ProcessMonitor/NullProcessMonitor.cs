// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// No-op implementation of <see cref="IProcessMonitor"/> used in tests and non-Windows builds.
/// Never fires <see cref="ForegroundProcessChanged"/>.
/// </summary>
public sealed class NullProcessMonitor : IProcessMonitor
{
    /// <inheritdoc/>
#pragma warning disable CS0067 // Event never used — intentional no-op implementation
    public event EventHandler<string>? ForegroundProcessChanged;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public void Start() { }

    /// <inheritdoc/>
    public void Stop() { }

    /// <inheritdoc/>
    public void Dispose() { }
}
