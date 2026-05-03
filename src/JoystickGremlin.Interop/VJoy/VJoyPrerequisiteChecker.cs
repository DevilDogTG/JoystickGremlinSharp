// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Performs a pure registry and file-system check to determine whether a compatible version
/// of vJoy (BrunnerInnovation fork) is installed — without loading the native DLL or calling
/// any P/Invoke functions.
/// </summary>
/// <remarks>
/// Call <see cref="Check"/> once during application startup (before the DI container resolves
/// <see cref="VJoyDeviceManager"/>) so that any missing-prerequisite dialog can be shown early.
/// </remarks>
public static class VJoyPrerequisiteChecker
{
    /// <summary>Minimum required vJoy product version string prefix (major.minor).</summary>
    private const string RequiredMajorMinor = "v2.2";

    /// <summary>URL of the BrunnerInnovation vJoy releases page shown in the warning dialog.</summary>
    public const string DownloadUrl = "https://github.com/BrunnerInnovation/vJoy/releases";

    /// <summary>
    /// Checks whether a compatible vJoy installation is present.
    /// </summary>
    /// <returns>
    /// A <see cref="VJoyPrerequisiteResult"/> describing the installation state.
    /// </returns>
    public static VJoyPrerequisiteResult Check()
    {
        var installDir = VJoyRegistryHelper.GetInstallDir();
        if (installDir is null)
            return VJoyPrerequisiteResult.NotInstalled();

        var dllPath = Path.Combine(installDir, "x64", "vJoyInterface.dll");
        if (!File.Exists(dllPath))
            return VJoyPrerequisiteResult.NotInstalled();

        var fileInfo = FileVersionInfo.GetVersionInfo(dllPath);
        var productVersion = fileInfo.ProductVersion ?? string.Empty;

        var isCompatible = productVersion.StartsWith(RequiredMajorMinor, StringComparison.OrdinalIgnoreCase);
        return new VJoyPrerequisiteResult(
            IsInstalled: true,
            IsCompatible: isCompatible,
            InstalledVersion: productVersion,
            InstallPath: installDir);
    }
}

/// <summary>
/// Describes the result of a vJoy prerequisite check.
/// </summary>
/// <param name="IsInstalled">Whether vJoy appears to be installed (registry + DLL present).</param>
/// <param name="IsCompatible">Whether the installed version meets the minimum requirement.</param>
/// <param name="InstalledVersion">Product version string read from the DLL, or <c>null</c> if not installed.</param>
/// <param name="InstallPath">Resolved installation directory, or <c>null</c> if not installed.</param>
public sealed record VJoyPrerequisiteResult(
    bool IsInstalled,
    bool IsCompatible,
    string? InstalledVersion,
    string? InstallPath)
{
    /// <summary>Gets a value indicating whether the prerequisite is fully satisfied.</summary>
    public bool IsOk => IsInstalled && IsCompatible;

    /// <summary>Returns a human-readable summary of why the check failed, or <c>null</c> if OK.</summary>
    public string? FailureReason => (IsInstalled, IsCompatible) switch
    {
        (false, _) => "vJoy is not installed.",
        (true, false) => $"vJoy {InstalledVersion} is installed, but v2.2.x or later (BrunnerInnovation fork) is required.",
        _ => null
    };

    internal static VJoyPrerequisiteResult NotInstalled() =>
        new(IsInstalled: false, IsCompatible: false, InstalledVersion: null, InstallPath: null);
}
