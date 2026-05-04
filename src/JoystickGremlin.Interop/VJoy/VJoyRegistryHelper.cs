// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Win32;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Shared helper for reading the vJoy installation location from the Windows registry.
/// </summary>
internal static class VJoyRegistryHelper
{
    /// <summary>
    /// Returns the vJoy installation directory (e.g. <c>C:\Program Files\vJoy\</c>),
    /// or <c>null</c> if vJoy is not installed or the registry entry is absent.
    /// </summary>
    internal static string? GetInstallDir()
    {
        const string uninstallKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        var result = SearchUninstallKey(Registry.LocalMachine.OpenSubKey(uninstallKey));
        if (result is not null)
            return result;

        // Some 32-bit installers write under WOW6432Node.
        return SearchUninstallKey(
            Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"));
    }

    /// <summary>
    /// Returns the vJoy configuration tool path when available.
    /// </summary>
    internal static string? GetConfigurationToolPath()
    {
        var installDir = GetInstallDir();
        if (string.IsNullOrWhiteSpace(installDir))
            return null;

        string[] candidates =
        [
            Path.Combine(installDir, "vJoyConf.exe"),
            Path.Combine(installDir, "x64", "vJoyConf.exe"),
            Path.Combine(installDir, "x86", "vJoyConf.exe"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? SearchUninstallKey(RegistryKey? root)
    {
        if (root is null)
            return null;

        using (root)
        {
            foreach (var name in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(name);
                if (sub?.GetValue("DisplayName") is string display &&
                    display.Contains("vJoy", StringComparison.OrdinalIgnoreCase) &&
                    sub.GetValue("InstallLocation") is string location)
                {
                    return location;
                }
            }
        }

        return null;
    }
}
