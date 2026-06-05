// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Default implementation of <see cref="IAutoLoadTriggerMigrator"/>.
/// Scans the profiles folder directly (in library scan order: root-level files first,
/// then category subfolders, alphabetical within each) so that profile files copied in
/// after the last library scan are still found.
/// </summary>
public sealed class AutoLoadTriggerMigrator(
    ISettingsService settingsService,
    IProfileLibrary profileLibrary,
    ILogger<AutoLoadTriggerMigrator> logger) : IAutoLoadTriggerMigrator
{
    private const string TriggersPropertyName = "AutoLoadTriggers";

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Applies 2-space indentation on write-back; all remaining profile content is
    // preserved verbatim by the JsonNode round-trip.
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
    };

    // Legacy (v11.x/v12.0) embedded trigger shape. Kept private to the migrator —
    // the rest of the codebase only knows the global AutoLoadTrigger.
    private sealed class LegacyTriggerDto
    {
        public ProcessMatchType MatchType { get; set; }
        public string ExecutableName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public bool RemainActiveOnFocusLoss { get; set; }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> DetectAsync(CancellationToken cancellationToken = default)
    {
        var found = new List<string>();
        foreach (var filePath in EnumerateProfileFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await ReadLegacyTriggersAsync(filePath, cancellationToken) is { Count: > 0 })
                {
                    found.Add(filePath);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Unreadable/invalid profile files are surfaced by the profile page, not here.
                logger.LogDebug(ex, "Skipping unreadable profile '{Path}' during legacy-trigger detection", filePath);
            }
        }
        return found;
    }

    /// <inheritdoc/>
    public async Task<AutoLoadMigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var failures = new List<AutoLoadMigrationFailure>();
        var pending = new List<(string FilePath, List<AutoLoadTrigger> Triggers)>();
        var settings = settingsService.Settings;

        // Pass 1 (read-only): collect embedded triggers, skipping any already lifted
        // into the global list (a previous run that saved settings but failed to strip
        // the profile file leaves such duplicates behind).
        foreach (var filePath in EnumerateProfileFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<LegacyTriggerDto> legacy;
            try
            {
                legacy = await ReadLegacyTriggersAsync(filePath, cancellationToken) ?? [];
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cannot read auto-load triggers from '{Path}'", filePath);
                failures.Add(new AutoLoadMigrationFailure(filePath, $"Cannot read file: {ex.Message}"));
                continue;
            }

            if (legacy.Count == 0)
            {
                continue;
            }

            var liftedFromFile = legacy
                .Select(t => new AutoLoadTrigger
                {
                    ProfilePath             = filePath,
                    MatchType               = t.MatchType,
                    ExecutableName          = t.ExecutableName,
                    ExecutablePath          = t.ExecutablePath,
                    IsEnabled               = t.IsEnabled,
                    AutoStart               = t.AutoStart,
                    RemainActiveOnFocusLoss = t.RemainActiveOnFocusLoss,
                })
                .Where(t => !settings.AutoLoadTriggers.Any(existing => IsSameTrigger(existing, t)))
                .ToList();

            pending.Add((filePath, liftedFromFile));
        }

        if (pending.Count == 0)
        {
            return new AutoLoadMigrationResult(0, 0, failures);
        }

        // Pass 2: persist the global list FIRST so a failed strip never loses triggers.
        // The list is replaced rather than mutated — the process monitor enumerates it
        // from a non-UI thread.
        var lifted = pending.SelectMany(p => p.Triggers).ToList();
        if (lifted.Count > 0)
        {
            settings.AutoLoadTriggers = [.. settings.AutoLoadTriggers, .. lifted];
            await settingsService.SaveAsync(cancellationToken);
        }

        // Pass 3: strip the embedded triggers from each profile file. A failure here is
        // recoverable — the trigger is already global, and the dedup in pass 1 keeps a
        // re-run from duplicating it.
        var migratedProfiles = 0;
        foreach (var (filePath, _) in pending)
        {
            try
            {
                await StripTriggersAsync(filePath, cancellationToken);
                migratedProfiles++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to strip embedded triggers from '{Path}'", filePath);
                failures.Add(new AutoLoadMigrationFailure(filePath, $"Triggers were imported, but the file could not be updated: {ex.Message}"));
            }
        }

        logger.LogInformation(
            "Auto-load trigger migration: lifted {TriggerCount} trigger(s) from {ProfileCount} profile(s), {FailureCount} failure(s).",
            lifted.Count, migratedProfiles, failures.Count);

        return new AutoLoadMigrationResult(migratedProfiles, lifted.Count, failures);
    }

    /// <summary>
    /// Enumerates profile JSON files in library scan order: root-level files first,
    /// then one-level category subfolders, alphabetical within each group.
    /// </summary>
    private IEnumerable<string> EnumerateProfileFiles()
    {
        var folder = profileLibrary.ProfilesFolder;
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
                                      .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }

        foreach (var subdir in Directory.GetDirectories(folder)
                                        .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.GetFiles(subdir, "*.json", SearchOption.TopDirectoryOnly)
                                          .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Lightweight read of just the embedded <c>AutoLoadTriggers</c> array.
    /// Returns <c>null</c> when the property is absent. Used by <see cref="DetectAsync"/>
    /// with errors swallowed (unreadable files are surfaced by the profile page, not here)
    /// and by <see cref="MigrateAsync"/> with errors propagated into the failure list.
    /// </summary>
    private async Task<List<LegacyTriggerDto>?> ReadLegacyTriggersAsync(
        string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        var triggersNode = FindTriggersProperty(root)?.Value;
        return triggersNode?.Deserialize<List<LegacyTriggerDto>>(_readOptions);
    }

    /// <summary>
    /// Removes the <c>AutoLoadTriggers</c> property from the profile file, preserving all
    /// other content verbatim (no full-profile deserialization, no legacy-format migration).
    /// </summary>
    private static async Task StripTriggersAsync(string filePath, CancellationToken cancellationToken)
    {
        JsonNode? root;
        await using (var stream = File.OpenRead(filePath))
        {
            root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        }

        if (FindTriggersProperty(root) is not { } property || root is not JsonObject obj)
        {
            return; // Nothing to strip — already migrated.
        }

        obj.Remove(property.Key);
        await File.WriteAllTextAsync(filePath, root.ToJsonString(_writeOptions), cancellationToken);
    }

    private static KeyValuePair<string, JsonNode?>? FindTriggersProperty(JsonNode? root)
    {
        if (root is not JsonObject obj)
        {
            return null;
        }

        // Profile JSON is read case-insensitively elsewhere; mirror that here.
        foreach (var property in obj)
        {
            if (string.Equals(property.Key, TriggersPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property;
            }
        }
        return null;
    }

    private static bool IsSameTrigger(AutoLoadTrigger a, AutoLoadTrigger b) =>
        string.Equals(a.ProfilePath, b.ProfilePath, StringComparison.OrdinalIgnoreCase)
        && a.MatchType == b.MatchType
        && string.Equals(a.ExecutableName, b.ExecutableName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ExecutablePath, b.ExecutablePath, StringComparison.OrdinalIgnoreCase);
}
