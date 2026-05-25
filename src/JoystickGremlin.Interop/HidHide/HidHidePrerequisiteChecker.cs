// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Win32;
using Nefarius.Drivers.HidHide;

namespace JoystickGremlin.Interop.HidHide;

/// <summary>
/// Checks whether the HidHide driver is installed and operational before the integration
/// is used for the first time.  Mirrors <c>VJoyPrerequisiteChecker</c> in pattern.
/// </summary>
public static class HidHidePrerequisiteChecker
{
    private const string HidHideUninstallKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HidHide";

    private const string HidHideWow64UninstallKey =
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\HidHide";

    private static readonly string[] CommonInstallDirs =
    [
        @"C:\Program Files\Nefarius Software Solutions\HidHide",
        @"C:\Program Files\Nefarius Software Solutions e.U\HidHide",
        @"C:\Program Files\Nefarius\HidHide",
    ];

    /// <summary>Download link shown to users when HidHide is missing.</summary>
    public const string DownloadUrl = "https://github.com/nefarius/HidHide/releases/latest";

    /// <summary>
    /// Performs a full prerequisite check and returns a <see cref="HidHidePrerequisiteResult"/>.
    /// Does not throw — all errors are captured in the result.
    /// </summary>
    public static HidHidePrerequisiteResult Check()
    {
        try
        {
            var svc = new HidHideControlService();
            var installed = svc.IsInstalled;
            if (!installed)
                return new HidHidePrerequisiteResult(IsInstalled: false, IsOperational: false);

            var operational = svc.IsOperational;
            return new HidHidePrerequisiteResult(IsInstalled: true, IsOperational: operational);
        }
        catch
        {
            return new HidHidePrerequisiteResult(IsInstalled: false, IsOperational: false);
        }
    }

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

    /// <summary>
    /// Returns the path to <c>HidHideClient.exe</c> (the GUI configuration tool),
    /// or <c>null</c> if HidHide is not installed or the executable cannot be found.
    /// </summary>
    public static string? GetConfigurationClientPath()
    {
        var installDir = ReadInstallDir();
        if (installDir is not null)
        {
            string[] candidates =
            [
                Path.Combine(installDir, "x64", "HidHideClient.exe"),
                Path.Combine(installDir, "HidHideClient.exe"),
            ];

            var found = candidates.FirstOrDefault(File.Exists);
            if (found is not null)
                return found;
        }

        foreach (var dir in CommonInstallDirs)
        {
            string[] candidates =
            [
                Path.Combine(dir, "x64", "HidHideClient.exe"),
                Path.Combine(dir, "HidHideClient.exe"),
            ];

            var found = candidates.FirstOrDefault(File.Exists);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static string? ReadInstallDir()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(HidHideUninstallKey, writable: false);
            if (key?.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
                return loc;

            using var wow64Key = Registry.LocalMachine.OpenSubKey(HidHideWow64UninstallKey, writable: false);
            return wow64Key?.GetValue("InstallLocation") as string;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Describes the result of a HidHide prerequisite check.
/// </summary>
/// <param name="IsInstalled">Whether the HidHide driver package is installed.</param>
/// <param name="IsOperational">Whether the driver device node is present and accessible.</param>
public sealed record HidHidePrerequisiteResult(bool IsInstalled, bool IsOperational)
{
    /// <summary>Gets a value indicating whether the prerequisite is fully satisfied.</summary>
    public bool IsOk => IsInstalled && IsOperational;

    /// <summary>Returns a human-readable summary of why the check failed, or <c>null</c> if OK.</summary>
    public string? FailureReason => (IsInstalled, IsOperational) switch
    {
        (false, _) => "HidHide is not installed.",
        (true, false) => "HidHide is installed but its driver device node is not operational. Try reinstalling HidHide.",
        _ => null,
    };
}
