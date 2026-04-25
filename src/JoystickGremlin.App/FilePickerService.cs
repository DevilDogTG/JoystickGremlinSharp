// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace JoystickGremlin.App;

/// <summary>
/// Implementation of <see cref="IFilePickerService"/> backed by Avalonia's <see cref="IStorageProvider"/>.
/// Call <see cref="SetTopLevel"/> once the main window is available (e.g. in the <c>Opened</c> event).
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    private TopLevel? _topLevel;

    /// <summary>Sets the <see cref="TopLevel"/> used for presenting file-picker dialogs.</summary>
    /// <param name="topLevel">The application's main window or top-level control.</param>
    public void SetTopLevel(TopLevel topLevel) => _topLevel = topLevel;

    /// <inheritdoc/>
    public async Task<string?> PickOpenFileAsync(string title, string filterName, string filterExtension)
    {
        if (_topLevel is null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = [filterExtension] }]
        };

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    /// <inheritdoc/>
    public async Task<string?> PickSaveFileAsync(string title, string filterName, string filterExtension, string defaultExtension)
    {
        if (_topLevel is null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = [filterExtension] }]
        };

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(options);
        return file?.Path.LocalPath;
    }
}
