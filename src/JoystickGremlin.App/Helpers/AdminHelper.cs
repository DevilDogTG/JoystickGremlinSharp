// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Security.Principal;

namespace JoystickGremlin.App.Helpers;

/// <summary>
/// Utility methods for detecting and requesting Windows administrator privileges.
/// </summary>
internal static class AdminHelper
{
    /// <summary>
    /// Gets a value indicating whether the current process is running with administrator privileges.
    /// </summary>
    internal static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Relaunches the current executable with the <c>runas</c> shell verb, triggering a UAC
    /// elevation prompt. The caller is responsible for exiting the current (non-elevated) process
    /// after this call returns.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current process executable path cannot be determined.
    /// </exception>
    internal static void RestartAsAdmin()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Could not determine the current process executable path.");

        Process.Start(new ProcessStartInfo(exePath)
        {
            Verb           = "runas",
            UseShellExecute = true,
        });
    }
}
