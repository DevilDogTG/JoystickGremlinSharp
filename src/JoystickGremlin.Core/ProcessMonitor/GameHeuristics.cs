// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Best-effort heuristics for guessing whether an executable is a game.
/// Used only to flag and sort entries in the process picker — it is deliberately conservative
/// and is not a reliable classifier (a game installed outside a known store folder won't be flagged).
/// </summary>
public static class GameHeuristics
{
    // Substrings (forward-slash, lower-case) commonly found in game install paths.
    private static readonly string[] GameStoreMarkers =
    [
        "steamapps",
        "steamlibrary",
        "epic games",
        "epicgames",
        "gog galaxy",
        "gog games",
        "origin games",
        "ea games",
        "/ea/",
        "ubisoft",
        "riot games",
        "battle.net",
        "/games/",
        "xboxgames",
        "windowsapps",
    ];

    /// <summary>
    /// Returns <c>true</c> when the executable path looks like it belongs to a game,
    /// based on well-known store/library folder names. Case- and separator-insensitive.
    /// </summary>
    /// <param name="executablePath">The full executable path.</param>
    public static bool IsLikelyGame(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var normalized = executablePath.Replace('\\', '/').ToLowerInvariant();
        foreach (var marker in GameStoreMarkers)
        {
            if (normalized.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
