// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Update;

/// <summary>
/// <see cref="IUpdateChecker"/> backed by the GitHub Releases REST API.
/// Fetches the repository's latest release (non-draft, non-prerelease by API contract)
/// and compares its tag against the running assembly version. Network, rate-limit, and
/// parsing failures never throw — they surface as <see cref="UpdateCheckStatus.Failed"/>
/// results so callers can render them as a status message.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker, IDisposable
{
    /// <summary>GitHub REST endpoint returning the latest published release.</summary>
    public const string LatestReleaseApiUrl =
        "https://api.github.com/repos/DevilDogTG/JoystickGremlinSharp/releases/latest";

    /// <summary>Browser fallback URL used when the API response carries no release link.</summary>
    public const string ReleasesPageUrl =
        "https://github.com/DevilDogTG/JoystickGremlinSharp/releases";

    /// <summary>Filename suffix that identifies the MSI installer asset published by CI.</summary>
    private const string InstallerAssetSuffix = "-Setup.msi";

    private readonly HttpClient _http;
    private readonly Version _currentVersion;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    /// <summary>
    /// Initializes a new instance comparing against the entry assembly version
    /// (stamped from <c>version.json</c> via <c>Directory.Build.props</c>).
    /// </summary>
    public GitHubUpdateChecker(ILogger<GitHubUpdateChecker> logger)
        : this(logger, new HttpClientHandler(), GetEntryAssemblyVersion())
    {
    }

    /// <summary>Test seam: injects the HTTP transport and the version to compare against.</summary>
    internal GitHubUpdateChecker(
        ILogger<GitHubUpdateChecker> logger,
        HttpMessageHandler handler,
        Version currentVersion)
    {
        _logger = logger;
        _currentVersion = Normalize(currentVersion);
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API rejects requests without a User-Agent.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"JoystickGremlinSharp/{_currentVersion}");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(LatestReleaseApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update check failed: GitHub API returned HTTP {StatusCode}", (int)response.StatusCode);
                return UpdateCheckResult.Failure($"GitHub returned HTTP {(int)response.StatusCode}.", _currentVersion);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseRelease(json.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // caller-initiated cancellation is not a failure
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            // OperationCanceledException here means the HttpClient timeout elapsed.
            _logger.LogWarning(ex, "Update check failed");
            var message = ex is OperationCanceledException ? "The request timed out." : ex.Message;
            return UpdateCheckResult.Failure(message, _currentVersion);
        }
    }

    private UpdateCheckResult ParseRelease(JsonElement release)
    {
        var tag = release.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
        if (!TryParseVersionTag(tag, out var latest))
        {
            _logger.LogWarning("Update check failed: unrecognized release tag {Tag}", tag);
            return UpdateCheckResult.Failure($"Unrecognized release tag '{tag}'.", _currentVersion);
        }

        var releaseUrl = release.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
        var notes = release.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;

        return new UpdateCheckResult
        {
            Status = latest > _currentVersion ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
            CurrentVersion = _currentVersion,
            LatestVersion = latest,
            ReleaseUrl = string.IsNullOrWhiteSpace(releaseUrl) ? ReleasesPageUrl : releaseUrl,
            DownloadUrl = FindInstallerAssetUrl(release),
            ReleaseNotes = notes,
        };
    }

    private static string? FindInstallerAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (name is null || !name.EndsWith(InstallerAssetSuffix, StringComparison.OrdinalIgnoreCase))
                continue;
            return asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() : null;
        }

        return null;
    }

    /// <summary>
    /// Parses a release tag like <c>v12.1.0</c> into a 3-component <see cref="Version"/>.
    /// Accepts an optional <c>v</c> prefix and ignores semver prerelease/build suffixes.
    /// </summary>
    internal static bool TryParseVersionTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];

        // "12.2.0-rc.1" / "12.2.0+build5" → "12.2.0"
        var suffix = text.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            text = text[..suffix];

        if (!Version.TryParse(text, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    /// <summary>
    /// Normalizes to exactly 3 components so a 4-part assembly version (12.1.0.0)
    /// compares equal to its 3-part release tag (v12.1.0).
    /// </summary>
    private static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    private static Version GetEntryAssemblyVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();
}
