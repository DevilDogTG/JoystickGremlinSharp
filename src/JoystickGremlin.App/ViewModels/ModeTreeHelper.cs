// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Shared helpers for DFS-ordered traversal of the mode hierarchy.
/// Used by both <see cref="ProfilePageViewModel"/> and <see cref="MainWindowViewModel"/>
/// to guarantee consistent ordering and indentation.
/// </summary>
internal static class ModeTreeHelper
{
    /// <summary>
    /// Returns all modes in DFS order (parents before their children).
    /// Orphaned modes (parent name not found) are appended at depth 0.
    /// Circular references are guarded by a visited set.
    /// </summary>
    public static IEnumerable<(JoystickGremlin.Core.Profile.Mode mode, int depth)> Flatten(
        List<JoystickGremlin.Core.Profile.Mode> modes)
    {
        var childrenOf = modes
            .Where(m => !string.IsNullOrEmpty(m.ParentModeName))
            .GroupBy(m => m.ParentModeName!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var rootModes = modes.Where(m => string.IsNullOrEmpty(m.ParentModeName)).ToList();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(JoystickGremlin.Core.Profile.Mode, int)>();

        void Visit(JoystickGremlin.Core.Profile.Mode mode, int depth)
        {
            if (!visited.Add(mode.Name)) return;
            result.Add((mode, depth));
            if (childrenOf.TryGetValue(mode.Name, out var children))
                foreach (var child in children)
                    Visit(child, depth + 1);
        }

        foreach (var root in rootModes)
            Visit(root, 0);

        foreach (var mode in modes.Where(m => !visited.Contains(m.Name)))
            result.Add((mode, 0));

        return result;
    }

    /// <summary>
    /// Builds <see cref="ModeTreeEntry"/> items in DFS order with non-breaking-space
    /// indentation so the ComboBox displays a visual hierarchy.
    /// Each depth level adds two non-breaking spaces (U+00A0) to the label.
    /// </summary>
    public static IEnumerable<ModeTreeEntry> BuildEntries(
        List<JoystickGremlin.Core.Profile.Mode> modes)
    {
        foreach (var (mode, depth) in Flatten(modes))
        {
            var indent = new string('\u00a0', depth * 2);
            yield return new ModeTreeEntry(mode.Name, indent + mode.Name);
        }
    }
}
