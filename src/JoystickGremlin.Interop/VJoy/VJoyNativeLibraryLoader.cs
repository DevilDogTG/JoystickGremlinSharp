// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Ensures that <c>vJoyInterface.dll</c> is loaded from the installed vJoy directory rather
/// than any older bundled version shipped with the application.  The installed DLL must match
/// the kernel-mode vJoy driver; using a mismatched DLL causes calls such as
/// <c>SetBtn</c> to return <c>true</c> while silently failing to update device state.
/// </summary>
/// <remarks>
/// Call <see cref="EnsureLoaded"/> once during application startup — before any P/Invoke
/// into <c>vJoyInterface.dll</c>.  Subsequent calls are no-ops.
/// </remarks>
internal static class VJoyNativeLibraryLoader
{
    private static volatile bool _registered;
    private static readonly object _lock = new();

    /// <summary>Gets the path of the vJoy DLL that was loaded, or <c>null</c> if not yet resolved.</summary>
    internal static string? LoadedDllPath { get; private set; }

    /// <summary>
    /// Registers a <see cref="NativeLibrary"/> resolver for <c>vJoyInterface.dll</c> that
    /// prefers the DLL found in the user's vJoy installation directory over any application-
    /// local copy.  Safe to call multiple times; only the first call takes effect.
    /// </summary>
    internal static void EnsureLoaded()
    {
        if (_registered) return;
        lock (_lock)
        {
            if (_registered) return;
            _registered = true;
        }

        NativeLibrary.SetDllImportResolver(
            typeof(VJoyNative).Assembly,
            ResolveLibrary);
    }

    private static IntPtr ResolveLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals("vJoyInterface.dll", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        // Prefer the DLL that ships with the installed vJoy driver so that the SDK version
        // always matches the kernel-mode driver version.
        var installDir = GetVJoyInstallDir();
        if (installDir is not null)
        {
            var dllPath = Path.Combine(installDir, "x64", "vJoyInterface.dll");
            if (File.Exists(dllPath) && NativeLibrary.TryLoad(dllPath, out var handle))
            {
                LoadedDllPath = dllPath;
                return handle;
            }
        }

        // Fall back to the default search order (bundled DLL, PATH, etc.).
        return IntPtr.Zero;
    }

    /// <summary>
    /// Reads the vJoy installation directory from the Windows uninstall registry.
    /// Returns <c>null</c> if vJoy is not installed or the registry entry is absent.
    /// </summary>
    private static string? GetVJoyInstallDir()
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
