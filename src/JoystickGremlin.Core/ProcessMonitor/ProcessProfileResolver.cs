// SPDX-License-Identifier: GPL-3.0-only

using System.Text.RegularExpressions;
using JoystickGremlin.Core.Configuration;

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Resolves a foreground executable path against a list of <see cref="ProcessProfileMapping"/> entries.
/// Uses exact-path matching first, then regex pattern matching.
/// </summary>
public static class ProcessProfileResolver
{
    /// <summary>
    /// Finds the first matching <see cref="ProcessProfileMapping"/> for the given executable path.
    /// </summary>
    /// <param name="executablePath">
    /// The normalized full path of the foreground executable (forward slashes, case-insensitive).
    /// </param>
    /// <param name="mappings">The ordered list of profile mappings to search.</param>
    /// <returns>
    /// The first enabled matching <see cref="ProcessProfileMapping"/>, or <c>null</c> if none match.
    /// </returns>
    public static ProcessProfileMapping? Resolve(
        string executablePath,
        IEnumerable<ProcessProfileMapping> mappings)
    {
        if (string.IsNullOrEmpty(executablePath)) return null;

        var normalized = Normalize(executablePath);
        var list = mappings.Where(m => m.IsEnabled).ToList();

        // Pass 1: exact path match (normalized, case-insensitive).
        foreach (var mapping in list)
        {
            if (string.Equals(Normalize(mapping.ExecutablePath), normalized, StringComparison.OrdinalIgnoreCase))
                return mapping;
        }

        // Pass 2: regex pattern match (first match wins).
        foreach (var mapping in list)
        {
            if (string.IsNullOrEmpty(mapping.ExecutablePath)) continue;
            try
            {
                if (Regex.IsMatch(normalized, mapping.ExecutablePath, RegexOptions.IgnoreCase))
                    return mapping;
            }
            catch (RegexParseException)
            {
                // Invalid regex — skip entry rather than crash.
            }
        }

        return null;
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}
