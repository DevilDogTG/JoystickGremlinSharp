// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Holds the currently active profile and notifies listeners when it changes.
/// Registered as a Singleton so all ViewModels share the same profile instance.
/// </summary>
public interface IProfileState
{
    /// <summary>Gets the currently active profile, or <c>null</c> if none is loaded.</summary>
    Profile? CurrentProfile { get; }

    /// <summary>Gets the file path of the current profile, or <c>null</c> if unsaved.</summary>
    string? FilePath { get; }

    /// <summary>
    /// Raised when the active profile identity changes (load, swap, or clear).
    /// In-place mutations of an already-loaded profile raise <see cref="ProfileModified"/>
    /// instead — UI subscribers that do expensive rebuilds (e.g. re-creating input rows
    /// or device lists) should listen here, not on <see cref="ProfileModified"/>.
    /// </summary>
    event EventHandler<Profile?>? ProfileChanged;

    /// <summary>
    /// Raised when the currently active profile's contents are modified in place
    /// (e.g. a binding/action is added, removed, or reordered) via
    /// <see cref="NotifyProfileModified"/>. The profile reference is unchanged;
    /// subscribers should perform incremental refreshes only.
    /// </summary>
    event EventHandler<Profile?>? ProfileModified;

    /// <summary>Raised when the file path changes (open, save-as, or clear).</summary>
    event EventHandler<string?>? FilePathChanged;

    /// <summary>
    /// Sets the active profile and optionally its file path, then raises
    /// <see cref="ProfileChanged"/> (and <see cref="FilePathChanged"/> if the path changed).
    /// </summary>
    void SetProfile(Profile profile, string? filePath = null);

    /// <summary>
    /// Updates the stored file path without replacing the profile.
    /// Raises <see cref="FilePathChanged"/> if the value is different.
    /// </summary>
    void UpdateFilePath(string? filePath);

    /// <summary>
    /// Fires <see cref="ProfileModified"/> with the current profile so subscribers
    /// can perform incremental refreshes after in-place edits (e.g. binding/action
    /// add, remove, reorder, config change). The profile reference is unchanged.
    /// </summary>
    void NotifyProfileModified();

    /// <summary>Clears the active profile and file path, then raises both events.</summary>
    void ClearProfile();
}
