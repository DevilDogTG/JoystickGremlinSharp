// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using JoystickGremlin.Interop.VJoy;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Modal dialog shown at startup when the vJoy prerequisite check fails.
/// Displays the failure reason and a link to the BrunnerInnovation vJoy releases page.
/// </summary>
public partial class VJoyWarningDialog : Window
{
    /// <summary>Required by the Avalonia XAML runtime loader.</summary>
    public VJoyWarningDialog() => InitializeComponent();

    /// <summary>
    /// Initializes the dialog with the prerequisite check result.
    /// </summary>
    public VJoyWarningDialog(VJoyPrerequisiteResult result) : this()
    {
        ReasonText.Text = result.FailureReason ?? "Unknown vJoy issue detected.";
    }

    /// <summary>
    /// Opens the BrunnerInnovation vJoy releases page in the default browser and
    /// closes the dialog so the user can install vJoy before restarting the application.
    /// </summary>
    private void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(VJoyPrerequisiteChecker.DownloadUrl)
        {
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
        Close();
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => Close();
}
