// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JoystickGremlin.Core.Exceptions;

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Persists and loads <see cref="Profile"/> instances as indented JSON files.
/// Automatically migrates legacy mode-based profiles on load.
/// </summary>
public sealed class ProfileRepository : IProfileRepository
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc/>
    public async Task<Profile> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var node = JsonNode.Parse(json);
            node = LegacyProfileMigrator.Migrate(node);
            var profile = node.Deserialize<Profile>(_options)
                          ?? throw new ProfileException($"Deserialization returned null for '{filePath}'.");
            return profile;
        }
        catch (ProfileException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProfileException($"Failed to load profile from '{filePath}'.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Profile profile, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, profile, _options, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ProfileException($"Failed to save profile to '{filePath}'.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Profile> LoadOrCreateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            return new Profile { Name = Path.GetFileNameWithoutExtension(filePath) };

        return await LoadAsync(filePath, cancellationToken);
    }
}
