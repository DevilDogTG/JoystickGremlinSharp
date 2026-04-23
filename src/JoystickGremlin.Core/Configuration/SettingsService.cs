// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// JSON-backed implementation of <see cref="ISettingsService"/>.
/// The settings file is stored in <c>%APPDATA%\JoystickGremlin\settings.json</c> by default.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    internal static readonly string DefaultSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JoystickGremlin",
        "settings.json");

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
    };

    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsService"/> using the default settings path.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public SettingsService(ILogger<SettingsService> logger)
        : this(logger, DefaultSettingsPath)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsService"/> with a custom settings path.
    /// Used for testing to redirect file I/O to a temporary directory.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="settingsPath">Absolute path to the settings JSON file.</param>
    internal SettingsService(ILogger<SettingsService> logger, string settingsPath)
    {
        _logger = logger;
        _settingsPath = settingsPath;
        Settings = new AppSettings();
    }

    /// <inheritdoc/>
    public AppSettings Settings { get; private set; }

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Start loading settings from {Path}", _settingsPath);

        if (!File.Exists(_settingsPath))
        {
            _logger.LogInformation("Settings file not found at {Path}. Using defaults.", _settingsPath);
            Settings = new AppSettings();
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            Settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _options, cancellationToken)
                       ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings from {Path}. Using defaults.", _settingsPath);
            Settings = new AppSettings();
        }

        _logger.LogTrace("Finished loading settings from {Path}", _settingsPath);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Start saving settings to {Path}", _settingsPath);

        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);

            await using var stream = File.Create(_settingsPath);
            await JsonSerializer.SerializeAsync(stream, Settings, _options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsPath);
            throw;
        }

        _logger.LogTrace("Finished saving settings to {Path}", _settingsPath);
    }
}
