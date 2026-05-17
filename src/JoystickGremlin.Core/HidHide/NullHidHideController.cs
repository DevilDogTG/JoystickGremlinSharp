// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// No-op <see cref="IHidHideController"/> used when HidHide is not installed or the Interop
/// layer has not registered a real implementation.
/// </summary>
internal sealed class NullHidHideController : IHidHideController
{
    /// <inheritdoc/>
    public bool IsInstalled => false;

    /// <inheritdoc/>
    public bool IsActive
    {
        get => false;
        set { /* no-op */ }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> BlockedInstanceIds => [];

    /// <inheritdoc/>
    public IReadOnlyList<string> ApplicationPaths => [];

    /// <inheritdoc/>
    public void AddBlockedInstance(string instanceId) { }

    /// <inheritdoc/>
    public void RemoveBlockedInstance(string instanceId) { }

    /// <inheritdoc/>
    public void AddApplicationPath(string fullPath) { }

    /// <inheritdoc/>
    public void RemoveApplicationPath(string fullPath) { }

    /// <inheritdoc/>
    public void Refresh() { }
}
