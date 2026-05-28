// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JoystickGremlin.App.ViewModels;
using JoystickGremlin.Core.ProcessMonitor;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Modal dialog that lets the user pick a running process. Closes with the selected
/// <see cref="RunningProcessInfo"/>, or <c>null</c> if cancelled.
/// </summary>
public partial class ProcessPickerDialog : Window
{
    /// <summary>Required by the Avalonia XAML runtime loader.</summary>
    public ProcessPickerDialog() => InitializeComponent();

    /// <summary>Initializes the dialog with its view model.</summary>
    public ProcessPickerDialog(ProcessPickerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>Handler for the Select button — confirms the current selection.</summary>
    private void SelectButton_Click(object? sender, RoutedEventArgs e) => Confirm();

    /// <summary>Handler for the Cancel button — closes the dialog with a null result.</summary>
    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(null);

    /// <summary>Handler for double-tap on the process list — shortcut for Select.</summary>
    private void ProcessList_DoubleTapped(object? sender, TappedEventArgs e) => Confirm();

    /// <summary>
    /// Closes the dialog with the currently selected process, or does nothing when no row is selected.
    /// </summary>
    private void Confirm()
    {
        if (DataContext is ProcessPickerViewModel { SelectedProcess: { } selected })
        {
            Close(selected);
        }
    }
}
