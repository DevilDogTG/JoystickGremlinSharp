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

    private void SelectButton_Click(object? sender, RoutedEventArgs e) => Confirm();

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void ProcessList_DoubleTapped(object? sender, TappedEventArgs e) => Confirm();

    private void Confirm()
    {
        if (DataContext is ProcessPickerViewModel { SelectedProcess: { } selected })
            Close(selected);
    }
}
