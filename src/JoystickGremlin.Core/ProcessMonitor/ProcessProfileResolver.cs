// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Profile;

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// Pairs a matched <see cref="ProcessTrigger"/> with the <see cref="ProfileEntry"/> that owns it.
/// </summary>
/// <param name="Profile">The profile whose trigger matched.</param>
/// <param name="Trigger">The matching trigger within that profile.</param>
public sealed record ProcessTriggerMatch(ProfileEntry Profile, ProcessTrigger Trigger);

/// <summary>
/// Resolves a foreground executable path against the <see cref="Profile.AutoLoadTriggers"/>
/// of every profile in the library.
/// </summary>
/// <remarks>
/// Iteration order is the library scan order (alphabetical by file path within each category,
/// root-level profiles before categorised profiles). Within each profile, triggers are evaluated
/// in declaration order. The first enabled trigger whose match criteria fit the executable wins.
/// </remarks>
public static class ProcessProfileResolver
{
    /// <summary>
    /// Finds the first matching trigger for the given executable across all profiles.
    /// </summary>
    /// <param name="executablePath">
    /// The full path of the foreground executable (any case, either path separator).
    /// </param>
    /// <param name="profiles">The ordered list of profile entries to search.</param>
    /// <returns>
    /// The first <see cref="ProcessTriggerMatch"/> whose trigger is enabled and matches, or
    /// <c>null</c> if none match.
    /// </returns>
    public static ProcessTriggerMatch? Resolve(
        string executablePath,
        IEnumerable<ProfileEntry> profiles)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return null;
        }

        var normalizedPath = Normalize(executablePath);
        var fileName = GetFileName(normalizedPath);

        foreach (var profile in profiles)
        {
            foreach (var trigger in profile.AutoLoadTriggers)
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
                            return new ProcessTriggerMatch(profile, trigger);
                        }
                        break;

                    case ProcessMatchType.ExecutablePath:
                        if (!string.IsNullOrEmpty(trigger.ExecutablePath)
                            && string.Equals(Normalize(trigger.ExecutablePath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            return new ProcessTriggerMatch(profile, trigger);
                        }
                        break;
                }
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
