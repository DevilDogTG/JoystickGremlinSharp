// SPDX-License-Identifier: GPL-3.0-only

using System.Threading.Tasks;
using Avalonia.Controls;
using JoystickGremlin.App.ViewModels;
using JoystickGremlin.App.Views;
using JoystickGremlin.Core.ProcessMonitor;

namespace JoystickGremlin.App.Services;

/// <summary>
/// Shows the modal process picker dialog and returns the user's selection.
/// Keeps Avalonia <see cref="Window"/> types out of view models.
/// </summary>
public interface IProcessPickerDialogService
{
    /// <summary>
    /// Opens the process picker dialog modally and returns the chosen process,
    /// or <c>null</c> if the user cancelled (or no owner window is set).
    /// </summary>
    Task<RunningProcessInfo?> PickProcessAsync();
}

/// <summary>
/// Default <see cref="IProcessPickerDialogService"/> implementation. Call <see cref="SetOwner"/>
/// once the main window is available (e.g. in the <c>Opened</c> event).
/// </summary>
public sealed class ProcessPickerDialogService : IProcessPickerDialogService
{
    private readonly IProcessEnumerator _enumerator;
    private Window? _owner;

    /// <summary>Initializes a new instance of <see cref="ProcessPickerDialogService"/>.</summary>
    public ProcessPickerDialogService(IProcessEnumerator enumerator) => _enumerator = enumerator;

    /// <summary>Sets the owner window used to present the modal dialog.</summary>
    public void SetOwner(Window owner) => _owner = owner;

    /// <inheritdoc/>
    public async Task<RunningProcessInfo?> PickProcessAsync()
    {
        if (_owner is null) return null;

        var dialog = new ProcessPickerDialog(new ProcessPickerViewModel(_enumerator));
        return await dialog.ShowDialog<RunningProcessInfo?>(_owner);
    }
}
