// SPDX-License-Identifier: GPL-3.0-only

using Nefarius.Drivers.HidHide;

namespace JoystickGremlin.Interop.HidHide;

/// <summary>
/// Checks whether the HidHide driver is installed and operational before the integration
/// is used for the first time.  Mirrors <c>VJoyPrerequisiteChecker</c> in pattern.
/// </summary>
public static class HidHidePrerequisiteChecker
{
    /// <summary>Download link shown to users when HidHide is missing.</summary>
    public const string DownloadUrl = "https://github.com/nefarius/HidHide/releases/latest";

    /// <summary>
    /// Gets a value indicating whether the HidHide driver is installed.
    /// Does not throw — returns <c>false</c> on any access failure.
    /// </summary>
    public static bool IsInstalled
    {
        get
        {
            try
            {
                var svc = new HidHideControlService();
                return svc.IsInstalled;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the HidHide driver node is present and operational.
    /// Does not throw — returns <c>false</c> on any access failure.
    /// </summary>
    public static bool IsOperational
    {
        get
        {
            try
            {
                var svc = new HidHideControlService();
                return svc.IsInstalled && svc.IsOperational;
            }
            catch
            {
                return false;
            }
        }
    }
}
