// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Resolves a foreground executable path against a list of <see cref="ProcessProfileMapping"/> entries.
/// Each mapping is matched either by executable file name or by full path (see
/// <see cref="ProcessMatchType"/>); the first enabled match in list order wins.
/// </summary>
public static class ProcessProfileResolver
{
    /// <summary>
    /// Finds the first matching <see cref="ProcessProfileMapping"/> for the given executable path.
    /// </summary>
    /// <param name="executablePath">
    /// The full path of the foreground executable (any case, either path separator).
    /// </param>
    /// <param name="mappings">The ordered list of profile mappings to search.</param>
    /// <returns>
    /// The first enabled matching <see cref="ProcessProfileMapping"/>, or <c>null</c> if none match.
    /// </returns>
    public static ProcessProfileMapping? Resolve(
        string executablePath,
        IEnumerable<ProcessProfileMapping> mappings)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return null;
        }

        var normalizedPath = Normalize(executablePath);
        var fileName = GetFileName(normalizedPath);

        foreach (var mapping in mappings)
        {
            if (!mapping.IsEnabled)
            {
                continue;
            }

            switch (mapping.MatchType)
            {
                case ProcessMatchType.ExecutableName:
                    if (!string.IsNullOrEmpty(mapping.ExecutableName)
                        && string.Equals(mapping.ExecutableName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapping;
                    }
                    break;

                case ProcessMatchType.ExecutablePath:
                    if (!string.IsNullOrEmpty(mapping.ExecutablePath)
                        && string.Equals(Normalize(mapping.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapping;
                    }
                    break;
            }
        }

        return null;
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimEnd('/');

    private static string GetFileName(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        return slash >= 0 ? normalizedPath[(slash + 1)..] : normalizedPath;
    }
}
