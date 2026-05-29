// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Scans a configurable folder for profile JSON files. Subfolders are categories.
/// </summary>
public sealed class ProfileLibrary : IProfileLibrary, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IProfileRepository _profileRepository;
    private readonly ILogger<ProfileLibrary> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyList<ProfileEntry> _entries = Array.Empty<ProfileEntry>();
    private volatile IReadOnlyList<string> _emptyCategories = Array.Empty<string>();

    private static readonly string DefaultFolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "JoystickGremlinSharp", "profiles");

    /// <summary>
    /// Initializes a new instance of <see cref="ProfileLibrary"/>.
    /// </summary>
    public ProfileLibrary(
        ISettingsService settingsService,
        IProfileRepository profileRepository,
        ILogger<ProfileLibrary> logger)
    {
        _settingsService   = settingsService;
        _profileRepository = profileRepository;
        _logger            = logger;
    }

    /// <inheritdoc/>
    public string ProfilesFolder
    {
        get
        {
            var configured = _settingsService.Settings.ProfilesFolderPath;
            return string.IsNullOrWhiteSpace(configured) ? DefaultFolderPath : configured;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProfileEntry> Entries => _entries;

    /// <inheritdoc/>
    public IReadOnlyList<string> EmptyCategories => _emptyCategories;

    /// <inheritdoc/>
    public event EventHandler? LibraryChanged;

    /// <inheritdoc/>
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ScanCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> CreateProfileAsync(string name, string? category = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var folder = category is not null
                ? Path.Combine(ProfilesFolder, SanitizeName(category))
                : ProfilesFolder;

            Directory.CreateDirectory(folder);

            var fileName = SanitizeName(name) + ".json";
            var filePath = Path.Combine(folder, fileName);

            if (File.Exists(filePath))
                throw new ProfileException($"Profile '{fileName}' already exists in '{folder}'.");

            var profile = new Profile { Name = name };
            await _profileRepository.SaveAsync(profile, filePath, cancellationToken);

            ScanCore();
            return filePath;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CreateCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sanitized = SanitizeName(name);
            if (string.IsNullOrWhiteSpace(sanitized))
                throw new ProfileException($"Category name '{name}' is invalid.");

            var folder = Path.Combine(ProfilesFolder, sanitized);
            var existedBefore = Directory.Exists(folder);
            Directory.CreateDirectory(folder);
            if (existedBefore)
                throw new ProfileException($"Category '{sanitized}' already exists.");

            _logger.LogInformation("Created profile category: {Folder}", folder);
            ScanCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task DeleteProfileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Delete requested for non-existent profile: {Path}", filePath);
                ScanCore();
                return;
            }

            File.Delete(filePath);
            _logger.LogInformation("Deleted profile: {Path}", filePath);
            ScanCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RenameProfileAsync(string filePath, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                throw new ProfileException($"Profile file not found: '{filePath}'.");

            var dir = Path.GetDirectoryName(filePath) ?? ProfilesFolder;
            var newFileName = SanitizeName(newName) + ".json";
            var newPath = Path.Combine(dir, newFileName);

            if (File.Exists(newPath) && !string.Equals(filePath, newPath, StringComparison.OrdinalIgnoreCase))
                throw new ProfileException($"A profile named '{newName}' already exists in this category.");

            File.Move(filePath, newPath);
            _logger.LogInformation("Renamed profile: {OldPath} → {NewPath}", filePath, newPath);
            ScanCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ScanCore()
    {
        var folder = ProfilesFolder;
        _logger.LogTrace("Scanning profiles folder: {Folder}", folder);

        if (!Directory.Exists(folder))
        {
            _entries = Array.Empty<ProfileEntry>();
            _emptyCategories = Array.Empty<string>();
            LibraryChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var found = new List<ProfileEntry>();
        var emptyCategories = new List<string>();

        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
                                      .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            found.Add(new ProfileEntry(name, null, file, ReadTriggers(file)));
        }

        foreach (var subdir in Directory.GetDirectories(folder)
                                        .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var category = Path.GetFileName(subdir);
            var files = Directory.GetFiles(subdir, "*.json", SearchOption.TopDirectoryOnly)
                                 .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                 .ToList();

            if (files.Count == 0)
            {
                emptyCategories.Add(category);
                continue;
            }

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                found.Add(new ProfileEntry(name, category, file, ReadTriggers(file)));
            }
        }

        _entries = found.AsReadOnly();
        _emptyCategories = emptyCategories.AsReadOnly();
        _logger.LogTrace("Found {Count} profiles, {EmptyCount} empty categories",
            _entries.Count, _emptyCategories.Count);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    // Lightweight read of only the AutoLoadTriggers section, avoiding the full
    // (potentially large) Bindings list and the legacy-profile migration path.
    private static readonly JsonSerializerOptions _triggerReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class TriggersOnlyDto
    {
        public List<ProcessTrigger>? AutoLoadTriggers { get; set; }
    }

    private IReadOnlyList<ProcessTrigger> ReadTriggers(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var dto = JsonSerializer.Deserialize<TriggersOnlyDto>(stream, _triggerReadOptions);
            var list = dto?.AutoLoadTriggers;
            return list is null ? Array.Empty<ProcessTrigger>() : list.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to read AutoLoadTriggers from '{Path}' — treating as empty.", filePath);
            return Array.Empty<ProcessTrigger>();
        }
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim('_', ' ');
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();
}
