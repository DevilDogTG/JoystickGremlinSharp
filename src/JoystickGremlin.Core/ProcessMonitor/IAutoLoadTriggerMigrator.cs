// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// A profile file that could not be migrated, with the reason.
/// </summary>
/// <param name="ProfilePath">Absolute path of the profile JSON file.</param>
/// <param name="Reason">Human-readable failure reason.</param>
public sealed record AutoLoadMigrationFailure(string ProfilePath, string Reason);

/// <summary>
/// Outcome of an <see cref="IAutoLoadTriggerMigrator.MigrateAsync"/> run.
/// </summary>
/// <param name="MigratedProfileCount">Number of profile files whose triggers were lifted and stripped.</param>
/// <param name="TriggerCount">Total number of triggers added to the global list.</param>
/// <param name="Failures">Profile files that could not be migrated; empty on full success.</param>
public sealed record AutoLoadMigrationResult(
    int MigratedProfileCount,
    int TriggerCount,
    IReadOnlyList<AutoLoadMigrationFailure> Failures);

/// <summary>
/// Migrates legacy (v11.x/v12.0) profile-embedded <c>AutoLoadTriggers</c> into the
/// global trigger list in <c>settings.json</c>.
/// </summary>
/// <remarks>
/// Runs automatically once at startup, and can be re-run manually from the Auto-load
/// page when legacy triggers are detected (e.g. a profile shared from an older
/// installation was copied into the library). Both operations are idempotent.
/// </remarks>
public interface IAutoLoadTriggerMigrator
{
    /// <summary>
    /// Scans the profiles folder for files that still contain a non-empty embedded
    /// <c>AutoLoadTriggers</c> array.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The absolute paths of profile files carrying legacy triggers, in scan order.</returns>
    Task<IReadOnlyList<string>> DetectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifts all embedded triggers into the global list and strips them from their
    /// profile files. The settings file is saved before any profile file is modified,
    /// so a partial failure never loses triggers; already-lifted triggers are skipped
    /// on retry, so a re-run never duplicates them.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of what was migrated and any per-file failures.</returns>
    Task<AutoLoadMigrationResult> MigrateAsync(CancellationToken cancellationToken = default);
}
