// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// Stores the identity of a physical HID device that should be hidden by HidHide
/// when the event pipeline is running.
/// </summary>
public sealed class HiddenDeviceEntry
{
    /// <summary>
    /// Gets or sets the Windows Device Instance ID (e.g.
    /// <c>HID\VID_054C&amp;PID_05C4\6&amp;1A2B3C4D&amp;0&amp;0000</c>).
    /// This is the identifier passed to HidHide's block list.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets a cached human-readable name for display purposes.</summary>
    public string FriendlyName { get; set; } = string.Empty;
}
