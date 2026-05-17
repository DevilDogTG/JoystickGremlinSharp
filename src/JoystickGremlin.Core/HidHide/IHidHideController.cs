// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Low-level abstraction over the HidHide driver communication channel.
/// Implementations may use IOCTL, the Nefarius NuGet library, or CLI fallback.
/// </summary>
public interface IHidHideController
{
    /// <summary>Gets a value indicating whether the HidHide driver is installed and reachable.</summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Gets or sets whether HidHide's global hiding gate is open.
    /// When <c>false</c>, all devices are visible to all applications even if on the block list.
    /// </summary>
    bool IsActive { get; set; }

    /// <summary>Gets the current list of blocked device instance IDs.</summary>
    IReadOnlyList<string> BlockedInstanceIds { get; }

    /// <summary>Gets the current list of application paths that bypass the block list.</summary>
    IReadOnlyList<string> ApplicationPaths { get; }

    /// <summary>Adds a device instance ID to the HidHide block list.</summary>
    /// <param name="instanceId">The Windows Device Instance ID to block.</param>
    void AddBlockedInstance(string instanceId);

    /// <summary>Removes a device instance ID from the HidHide block list.</summary>
    /// <param name="instanceId">The Windows Device Instance ID to unblock.</param>
    void RemoveBlockedInstance(string instanceId);

    /// <summary>Adds an application executable path to the bypass list.</summary>
    /// <param name="fullPath">The full path to the application executable.</param>
    void AddApplicationPath(string fullPath);

    /// <summary>Removes an application executable path from the bypass list.</summary>
    /// <param name="fullPath">The full path to the application executable.</param>
    void RemoveApplicationPath(string fullPath);

    /// <summary>Re-reads the current state from the driver.</summary>
    void Refresh();
}
