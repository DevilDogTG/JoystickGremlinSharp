// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Resolves a foreground executable path against the global auto-load trigger list
/// (<c>AppSettings.AutoLoadTriggers</c>).
/// </summary>
/// <remarks>
/// Triggers are evaluated in list order; the first enabled trigger whose match criteria
/// fit the executable wins. The matched trigger carries the profile to load via
/// <see cref="AutoLoadTrigger.ProfilePath"/>.
/// </remarks>
public static class ProcessProfileResolver
{
    /// <summary>
    /// Finds the first matching trigger for the given executable.
    /// </summary>
    /// <param name="executablePath">
    /// The full path of the foreground executable (any case, either path separator).
    /// </param>
    /// <param name="triggers">The ordered global trigger list to search.</param>
    /// <returns>
    /// The first <see cref="AutoLoadTrigger"/> that is enabled and matches, or
    /// <c>null</c> if none match.
    /// </returns>
    public static AutoLoadTrigger? Resolve(
        string executablePath,
        IEnumerable<AutoLoadTrigger> triggers)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return null;
        }

        var normalizedPath = Normalize(executablePath);
        var fileName = GetFileName(normalizedPath);

        foreach (var trigger in triggers)
        {
            if (!trigger.IsEnabled)
            {
                continue;
            }

            switch (trigger.MatchType)
            {
                case ProcessMatchType.ExecutableName:
                    if (!string.IsNullOrEmpty(trigger.ExecutableName)
                        && string.Equals(trigger.ExecutableName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return trigger;
                    }
                    break;

                case ProcessMatchType.ExecutablePath:
                    if (!string.IsNullOrEmpty(trigger.ExecutablePath)
                        && string.Equals(Normalize(trigger.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return trigger;
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
