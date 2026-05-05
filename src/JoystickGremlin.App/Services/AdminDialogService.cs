// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using JoystickGremlin.App.Views;

namespace JoystickGremlin.App.Services;

/// <summary>
/// Shows the <see cref="EmuWheelAdminDialog"/> modal dialog when the active process is not
/// running as administrator but EmuWheel identity spoofing is required.
/// Call <see cref="SetTopLevel"/> once the main window is available (e.g. in the <c>Opened</c> event).
/// </summary>
public sealed class AdminDialogService
{
    private Window? _topLevel;

    /// <summary>Sets the owner <see cref="Window"/> used to show modal dialogs.</summary>
    public void SetTopLevel(Window topLevel) => _topLevel = topLevel;

    /// <summary>
    /// Shows the EmuWheel administrator-required dialog modally and returns whether the user
    /// chose to restart as administrator.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the user clicked "Restart as Administrator"; <c>false</c> if the user
    /// chose to continue without EmuWheel or if the dialog could not be shown.
    /// </returns>
    public async Task<bool> ShowEmuWheelAdminDialogAsync()
    {
        if (_topLevel is null)
            return false;

        var dialog = new EmuWheelAdminDialog();
        var result = await dialog.ShowDialog<bool?>(_topLevel);
        return result == true;
    }
}
