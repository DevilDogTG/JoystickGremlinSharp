// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using JoystickGremlin.Interop.HidHide;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Modal dialog shown at startup when HidHide integration is enabled but the driver is
/// not installed or not operational.
/// </summary>
public partial class HidHideWarningDialog : Window
{
    /// <summary>Required by the Avalonia XAML runtime loader.</summary>
    public HidHideWarningDialog() => InitializeComponent();

    private void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(HidHidePrerequisiteChecker.DownloadUrl)
        {
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
        Close();
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => Close();
}
