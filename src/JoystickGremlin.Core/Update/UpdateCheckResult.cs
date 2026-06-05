// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Update;

/// <summary>Outcome category of an update check.</summary>
public enum UpdateCheckStatus
{
    /// <summary>The running version is the latest published release (or newer).</summary>
    UpToDate,

    /// <summary>A newer release is available.</summary>
    UpdateAvailable,

    /// <summary>The check could not be completed (network error, rate limit, bad response).</summary>
    Failed,
}

/// <summary>
/// Result of an <see cref="IUpdateChecker.CheckForUpdateAsync"/> call.
/// </summary>
public sealed record UpdateCheckResult
{
    /// <summary>Gets the outcome category of the check.</summary>
    public required UpdateCheckStatus Status { get; init; }

    /// <summary>Gets the running application version the check compared against.</summary>
    public Version? CurrentVersion { get; init; }

    /// <summary>Gets the latest published release version. Null when the check failed.</summary>
    public Version? LatestVersion { get; init; }

    /// <summary>Gets the browser URL of the latest release page. Null when the check failed.</summary>
    public string? ReleaseUrl { get; init; }

    /// <summary>
    /// Gets the direct download URL of the installer asset, when the release carries one.
    /// Falls back to <see cref="ReleaseUrl"/> consumers when null.
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Gets the release notes (Markdown body) of the latest release, if any.</summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>Gets a human-readable failure description when <see cref="Status"/> is <see cref="UpdateCheckStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a <see cref="UpdateCheckStatus.Failed"/> result with the given description.</summary>
    /// <param name="message">Human-readable failure description.</param>
    /// <param name="currentVersion">The running application version the check compared against.</param>
    /// <returns>A failed result.</returns>
    internal static UpdateCheckResult Failure(string message, Version currentVersion) => new()
    {
        Status = UpdateCheckStatus.Failed,
        CurrentVersion = currentVersion,
        ErrorMessage = message,
    };
}
