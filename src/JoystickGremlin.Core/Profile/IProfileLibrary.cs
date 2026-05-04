// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Discovers and manages profile files in the configured profiles folder.
/// Subfolders are treated as categories.
/// </summary>
public interface IProfileLibrary
{
    /// <summary>Gets the resolved absolute path to the profiles folder.</summary>
    string ProfilesFolder { get; }

    /// <summary>Gets the currently discovered profile entries.</summary>
    IReadOnlyList<ProfileEntry> Entries { get; }

    /// <summary>Raised when the library entries change (after a scan or file operation).</summary>
    event EventHandler? LibraryChanged;

    /// <summary>Rescans the profiles folder and refreshes <see cref="Entries"/>.</summary>
    Task ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new empty profile JSON file and returns the path.
    /// </summary>
    /// <param name="name">The profile name (used as the file name, sanitized).</param>
    /// <param name="category">Optional category (subfolder name). Pass <c>null</c> for root level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file path of the newly created profile.</returns>
    Task<string> CreateProfileAsync(string name, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes the profile file at the given path and rescans.</summary>
    Task DeleteProfileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Renames the profile file (file name only, not path) and rescans.</summary>
    Task RenameProfileAsync(string filePath, string newName, CancellationToken cancellationToken = default);
}
