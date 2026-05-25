// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace JoystickGremlin.Interop.HidHide;

/// <summary>
/// Shells out to <c>HidHideCLI.exe</c> as a fallback when the IOCTL communication path fails.
/// Looks for the executable in the HidHide installation directory (registry) or PATH.
/// <para>
/// Write operations first attempt the CLI with the current (non-elevated) token. If Windows
/// rejects the launch with <c>ERROR_ELEVATION_REQUIRED</c> (740), the CLI is re-launched via
/// <c>ShellExecute runas</c> which shows a UAC prompt. If the user cancels the prompt
/// (<c>ERROR_CANCELLED</c> 1223), a <see cref="HidHideElevationCancelledException"/> is thrown
/// so callers can skip the operation gracefully.
/// </para>
/// </summary>
internal sealed class HidHideCliFallback
{
    private const string HidHideUninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HidHide";
    private const string HidHideWow64UninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\HidHide";
    private const string HidHideCliExeName = "HidHideCLI.exe";

    // Win32 error codes relevant to UAC elevation
    private const int ErrorElevationRequired = 740;
    private const int ErrorCancelled = 1223;

    // Common install paths used when the uninstall registry key is absent
    private static readonly string[] CommonInstallDirs =
    [
        @"C:\Program Files\Nefarius Software Solutions\HidHide",
        @"C:\Program Files\Nefarius Software Solutions e.U\HidHide",
        @"C:\Program Files\Nefarius\HidHide",
    ];

    private readonly ILogger<HidHideCliFallback> _logger;
    private string? _cachedCliPath;

    public HidHideCliFallback(ILogger<HidHideCliFallback> logger)
    {
        _logger = logger;
    }

    /// <summary>Enables or disables the HidHide cloaking gate via CLI.</summary>
    public void SetActive(bool active)
    {
        Run(active ? "--cloak-on" : "--cloak-off");
    }

    /// <summary>Adds a device instance ID to the block list via CLI.</summary>
    public void AddBlockedInstance(string instanceId)
    {
        Run("--dev-hide", instanceId);
    }

    /// <summary>Removes a device instance ID from the block list via CLI.</summary>
    public void RemoveBlockedInstance(string instanceId)
    {
        Run("--dev-unhide", instanceId);
    }

    /// <summary>Adds an application path to the bypass list via CLI.</summary>
    public void AddApplicationPath(string fullPath)
    {
        Run("--app-reg", $"\"{fullPath}\"");
    }

    /// <summary>Removes an application path from the bypass list via CLI.</summary>
    public void RemoveApplicationPath(string fullPath)
    {
        Run("--app-unreg", $"\"{fullPath}\"");
    }

    private void Run(string command, string? argument = null)
    {
        var cliPath = FindCliPath();
        if (cliPath is null)
            throw new InvalidOperationException("HidHideCLI.exe not found. Is HidHide installed?");

        var args = argument is null ? command : $"{command} {argument}";
        _logger.LogDebug("HidHide CLI: {CliPath} {Args}", cliPath, args);

        // Attempt non-elevated first. If the app is already running as admin this succeeds
        // immediately. If the CLI requires elevation the OS returns ERROR_ELEVATION_REQUIRED.
        try
        {
            RunProcess(cliPath, args, useShellExecute: false);
            return;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorElevationRequired)
        {
            _logger.LogInformation("HidHide CLI requires elevation — showing UAC prompt");
        }

        // Re-try via ShellExecute runas, which triggers a Windows UAC dialog just for this
        // CLI subprocess. If the user clicks "No", Win32Exception 1223 is thrown.
        try
        {
            RunProcess(cliPath, args, useShellExecute: true);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new HidHideElevationCancelledException(
                "User declined the administrator elevation request for HidHide.", ex);
        }
    }

    private void RunProcess(string cliPath, string args, bool useShellExecute)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = args,
                UseShellExecute = useShellExecute,
                Verb = useShellExecute ? "runas" : null,
                // Stream redirection is unavailable when UseShellExecute=true.
                RedirectStandardError = !useShellExecute,
                CreateNoWindow = !useShellExecute,
            }
        };

        process.Start();

        string? stderr = useShellExecute ? null : process.StandardError.ReadToEnd();
        process.WaitForExit(5000);

        if (process.ExitCode != 0)
        {
            var msg = string.IsNullOrWhiteSpace(stderr)
                ? $"HidHideCLI exited with code {process.ExitCode}"
                : $"HidHideCLI exited with code {process.ExitCode}: {stderr.Trim()}";
            _logger.LogError("HidHide CLI failure: {Message}", msg);
            throw new InvalidOperationException(msg);
        }
    }

    private string? FindCliPath()
    {
        if (_cachedCliPath is not null && File.Exists(_cachedCliPath))
            return _cachedCliPath;

        // Try registry-derived install directory first
        var installDir = ReadInstallDir();
        if (installDir is not null)
        {
            var candidate = Path.Combine(installDir, "x64", HidHideCliExeName);
            if (File.Exists(candidate))
            {
                _cachedCliPath = candidate;
                return candidate;
            }

            candidate = Path.Combine(installDir, HidHideCliExeName);
            if (File.Exists(candidate))
            {
                _cachedCliPath = candidate;
                return candidate;
            }
        }

        // Try well-known install paths (handles missing/orphaned uninstall registry key)
        foreach (var dir in CommonInstallDirs)
        {
            var candidate = Path.Combine(dir, "x64", HidHideCliExeName);
            if (File.Exists(candidate))
            {
                _logger.LogDebug("HidHide CLI found at common path: {Path}", candidate);
                _cachedCliPath = candidate;
                return candidate;
            }

            candidate = Path.Combine(dir, HidHideCliExeName);
            if (File.Exists(candidate))
            {
                _logger.LogDebug("HidHide CLI found at common path: {Path}", candidate);
                _cachedCliPath = candidate;
                return candidate;
            }
        }

        // Try PATH resolution
        var fromPath = FindInPath(HidHideCliExeName);
        if (fromPath is not null)
        {
            _cachedCliPath = fromPath;
            return fromPath;
        }

        _logger.LogWarning("HidHideCLI.exe not found in registry install dir, common paths, or PATH");
        return null;
    }

    private static string? ReadInstallDir()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(HidHideUninstallKey, writable: false);
            if (key?.GetValue("InstallLocation") is string installLocation)
                return installLocation;

            // Also try WOW6432Node for 32-bit installer on 64-bit OS
            using var wow64Key = Registry.LocalMachine.OpenSubKey(HidHideWow64UninstallKey, writable: false);
            return wow64Key?.GetValue("InstallLocation") as string;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindInPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir, exeName);
            if (File.Exists(full))
                return full;
        }
        return null;
    }
}
