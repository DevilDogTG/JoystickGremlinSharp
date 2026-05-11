// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Win32;

namespace JoystickGremlin.Interop.JgsWheel;

/// <summary>
/// Detects whether the JGS Wheel driver (<c>jgswheel.sys</c>) is installed and which
/// device slots it has been configured for.
/// </summary>
/// <remarks>
/// The check is purely a registry probe — it does not load the user-mode interface DLL,
/// so it is safe to call before the DI container is built or before the driver has ever
/// been installed. The driver registers itself under
/// <c>HKLM\SYSTEM\CurrentControlSet\Services\jgswheel</c> via its INF.
/// </remarks>
public static class JgsWheelPrerequisiteChecker
{
    /// <summary>Service name used by the JGS Wheel driver INF.</summary>
    public const string ServiceName = "jgswheel";

    /// <summary>Default user-mode interface DLL filename for JGS Wheel.</summary>
    public const string InterfaceDllName = "JgsWheelInterface.dll";

    /// <summary>Registry path where the driver registers per-device parameters.</summary>
    public const string ParametersKeyPath =
        @"SYSTEM\CurrentControlSet\Services\jgswheel\Parameters";

    /// <summary>
    /// Probes the registry for an installed JGS Wheel driver.
    /// Returns a result describing whether the service exists and whether the user-mode
    /// interface DLL is available beside the application.
    /// </summary>
    public static JgsWheelPrerequisiteResult Check()
    {
        if (!OperatingSystem.IsWindows())
            return JgsWheelPrerequisiteResult.NotInstalled();

        try
        {
            using var serviceKey = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
            if (serviceKey is null)
                return JgsWheelPrerequisiteResult.NotInstalled();

            var imagePath = serviceKey.GetValue("ImagePath") as string;
            var interfaceDll = TryResolveInterfaceDll();

            return new JgsWheelPrerequisiteResult(
                IsInstalled: true,
                ImagePath: imagePath,
                InterfaceDllPath: interfaceDll);
        }
        catch
        {
            return JgsWheelPrerequisiteResult.NotInstalled();
        }
    }

    /// <summary>
    /// Locates the user-mode interface DLL. Search order:
    /// 1) Beside the executing assembly.
    /// 2) Same drive as the driver service ImagePath, under <c>x64\</c>.
    /// </summary>
    private static string? TryResolveInterfaceDll()
    {
        var asmDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(asmDir, InterfaceDllName);
        return File.Exists(candidate) ? candidate : null;
    }
}

/// <summary>
/// Result of a JGS Wheel driver prerequisite probe.
/// </summary>
/// <param name="IsInstalled">Whether the driver service is registered with Windows.</param>
/// <param name="ImagePath">Driver service image path (sys file location), if registered.</param>
/// <param name="InterfaceDllPath">Path to the user-mode interface DLL, if found.</param>
public sealed record JgsWheelPrerequisiteResult(
    bool IsInstalled,
    string? ImagePath,
    string? InterfaceDllPath)
{
    /// <summary>Gets a value indicating whether the prerequisite is fully satisfied.</summary>
    public bool IsOk => IsInstalled && InterfaceDllPath is not null;

    internal static JgsWheelPrerequisiteResult NotInstalled() =>
        new(IsInstalled: false, ImagePath: null, InterfaceDllPath: null);
}
