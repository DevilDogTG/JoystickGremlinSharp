// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.HidHide;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.HidHide;

/// <summary>
/// CLI-backed implementation of <see cref="IHidHideService"/> that shells out to
/// <c>HidHideCLI.exe</c> shipped by Nefarius HidHide.
/// </summary>
/// <remarks>
/// Verified CLI flags (HidHide ≥ 1.5):
/// <list type="bullet">
///   <item><c>--dev-list</c> — print all known device instances + their hidden state</item>
///   <item><c>--dev-hide "&lt;path&gt;"</c> / <c>--dev-unhide "&lt;path&gt;"</c></item>
///   <item><c>--app-list</c> — print whitelist</item>
///   <item><c>--app-reg "&lt;exe&gt;"</c> / <c>--app-unreg "&lt;exe&gt;"</c></item>
///   <item><c>--cloak-on</c> / <c>--cloak-off</c></item>
/// </list>
/// </remarks>
public sealed class HidHideCliService : IHidHideService
{
    private const string ExecutableName = "HidHideCLI.exe";

    private static readonly string[] DefaultProbePaths =
    [
        @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
        @"C:\Program Files\Nefarius Software Solutions B.V\HidHide\x64\HidHideCLI.exe",
    ];

    private readonly ISettingsService _settings;
    private readonly ILogger<HidHideCliService> _logger;

    /// <summary>Initializes a new <see cref="HidHideCliService"/>.</summary>
    public HidHideCliService(ISettingsService settings, ILogger<HidHideCliService> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public HidHideStatus GetStatus()
    {
        var path = ResolveCliPath();
        if (path is null)
            return HidHideStatus.NotInstalled();

        try
        {
            var version = FileVersionInfo.GetVersionInfo(path).ProductVersion;
            return new HidHideStatus(IsInstalled: true, CliPath: path, Version: version);
        }
        catch
        {
            return new HidHideStatus(IsInstalled: true, CliPath: path, Version: null);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HidHideDeviceEntry>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(["--dev-list"], cancellationToken);
        return ParseDeviceList(output);
    }

    /// <inheritdoc />
    public Task HideDeviceAsync(string instancePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);
        return RunAsync(["--dev-hide", instancePath], cancellationToken);
    }

    /// <inheritdoc />
    public Task UnhideDeviceAsync(string instancePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);
        return RunAsync(["--dev-unhide", instancePath], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HidHideAppWhitelistEntry>> ListWhitelistAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(["--app-list"], cancellationToken);
        return ParseWhitelist(output);
    }

    /// <inheritdoc />
    public Task AddWhitelistEntryAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        return RunAsync(["--app-reg", imagePath], cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveWhitelistEntryAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        return RunAsync(["--app-unreg", imagePath], cancellationToken);
    }

    /// <inheritdoc />
    public Task SetCloakEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        return RunAsync([enabled ? "--cloak-on" : "--cloak-off"], cancellationToken);
    }

    private string? ResolveCliPath()
    {
        var configured = _settings.Settings.HidHideCliPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        foreach (var candidate in DefaultProbePaths)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private async Task<string> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var path = ResolveCliPath()
            ?? throw new InvalidOperationException(
                "HidHide CLI not found. Install HidHide from https://github.com/nefarius/HidHide/releases " +
                "or set AppSettings.HidHideCliPath.");

        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{path}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "HidHideCLI {Args} exited with {Code}: {Stderr}",
                string.Join(" ", args), process.ExitCode, stderr);
            throw new InvalidOperationException(
                $"HidHideCLI {string.Join(" ", args)} failed (exit {process.ExitCode}): {stderr.Trim()}");
        }

        return stdout;
    }

    /// <summary>
    /// Parses lines like <c>"HID\VID_046D&amp;PID_C24F\6&amp;1abc... [hidden]"</c>.
    /// </summary>
    internal static IReadOnlyList<HidHideDeviceEntry> ParseDeviceList(string output)
    {
        var list = new List<HidHideDeviceEntry>();
        if (string.IsNullOrWhiteSpace(output)) return list;

        foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var hidden = line.Contains("[hidden]", StringComparison.OrdinalIgnoreCase);
            var path = line
                .Replace("[hidden]", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("[visible]", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            // CLI may print "<path> | <friendly>" — split on " | " when present.
            string display = path;
            var pipeIdx = path.IndexOf(" | ", StringComparison.Ordinal);
            if (pipeIdx > 0)
            {
                display = path[(pipeIdx + 3)..].Trim();
                path = path[..pipeIdx].Trim();
            }

            if (path.Length > 0)
                list.Add(new HidHideDeviceEntry(path, display, hidden));
        }

        return list;
    }

    internal static IReadOnlyList<HidHideAppWhitelistEntry> ParseWhitelist(string output)
    {
        var list = new List<HidHideAppWhitelistEntry>();
        if (string.IsNullOrWhiteSpace(output)) return list;

        foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            list.Add(new HidHideAppWhitelistEntry(line));
        }

        return list;
    }
}
