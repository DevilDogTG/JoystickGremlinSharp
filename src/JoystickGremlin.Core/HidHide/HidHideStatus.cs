// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Describes the current state of the HidHide integration.
/// </summary>
public enum HidHideStatus
{
    /// <summary>HidHide integration is disabled by the user (master toggle off).</summary>
    Disabled,

    /// <summary>HidHide driver is not installed on this machine.</summary>
    NotInstalled,

    /// <summary>HidHide is installed and the integration is ready to activate.</summary>
    Ready,

    /// <summary>HidHide is currently active — selected devices are being hidden.</summary>
    Active,

    /// <summary>An error occurred communicating with the HidHide driver.</summary>
    Error,
}
