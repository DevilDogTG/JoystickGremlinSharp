// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.App;

/// <summary>
/// Provides platform file-picker dialogs, abstracting Avalonia's TopLevel dependency
/// so ViewModels can open/save files without a direct UI reference.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Shows a file-open picker dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filterName">Human-readable file-type label.</param>
    /// <param name="filterExtension">Glob pattern, e.g. <c>*.json</c>.</param>
    /// <returns>The selected local file path, or <c>null</c> if cancelled.</returns>
    Task<string?> PickOpenFileAsync(string title, string filterName, string filterExtension);

    /// <summary>
    /// Shows a file-save picker dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filterName">Human-readable file-type label.</param>
    /// <param name="filterExtension">Glob pattern, e.g. <c>*.json</c>.</param>
    /// <param name="defaultExtension">Extension appended when none is typed, e.g. <c>json</c>.</param>
    /// <returns>The chosen local file path, or <c>null</c> if cancelled.</returns>
    Task<string?> PickSaveFileAsync(string title, string filterName, string filterExtension, string defaultExtension);
}
