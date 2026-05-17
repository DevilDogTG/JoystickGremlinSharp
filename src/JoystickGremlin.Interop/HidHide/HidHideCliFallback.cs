// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace JoystickGremlin.Interop.HidHide;

/// <summary>
/// Shells out to <c>HidHideCLI.exe</c> as a fallback when the IOCTL communication path fails.
/// Looks for the executable in the HidHide installation directory (registry) or PATH.
/// </summary>
internal sealed class HidHideCliFallback
{
    private const string HidHideUninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HidHide";
    private const string HidHideCliExeName = "HidHideCLI.exe";

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

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = args,
                UseShellExecute = false,
                // Do NOT redirect stdout — reading it is unnecessary and leaving it unread
                // while redirected can deadlock the process if its stdout buffer fills.
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var stderr = process.StandardError.ReadToEnd();
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

        // Try PATH resolution
        var fromPath = FindInPath(HidHideCliExeName);
        if (fromPath is not null)
        {
            _cachedCliPath = fromPath;
            return fromPath;
        }

        _logger.LogWarning("HidHideCLI.exe not found in registry install dir or PATH");
        return null;
    }

    private static string? ReadInstallDir()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(HidHideUninstallKey, writable: false);
            return key?.GetValue("InstallLocation") as string;
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
