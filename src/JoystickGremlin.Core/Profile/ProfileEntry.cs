// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Represents a discovered profile in the profiles library folder.
/// </summary>
/// <param name="Name">The profile display name (file name without extension).</param>
/// <param name="Category">The category (immediate subfolder name relative to profiles root), or <c>null</c> for root-level profiles.</param>
/// <param name="FilePath">The absolute path to the profile JSON file.</param>
/// <param name="AutoLoadTriggers">
/// Snapshot of the profile's <see cref="Profile.AutoLoadTriggers"/> at the time of the last library scan.
/// Empty when the file is unreadable or contains no triggers. Refreshed on each <see cref="IProfileLibrary.ScanAsync"/>.
/// </param>
public sealed record ProfileEntry(
    string Name,
    string? Category,
    string FilePath,
    IReadOnlyList<ProcessTrigger> AutoLoadTriggers)
{
    /// <summary>Gets the display label shown in UI selectors: "Category/Name" or "Name" when no category.</summary>
    public string DisplayLabel => Category is not null ? $"{Category}/{Name}" : Name;
}
