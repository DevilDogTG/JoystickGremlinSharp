// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Startup;

/// <summary>
/// No-op implementation of <see cref="IStartupService"/> used on non-Windows platforms
/// or when the Interop layer has not registered a real implementation.
/// </summary>
public sealed class NullStartupService : IStartupService
{
    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <inheritdoc/>
    public void Enable(string executablePath) { }

    /// <inheritdoc/>
    public void Disable() { }
}
