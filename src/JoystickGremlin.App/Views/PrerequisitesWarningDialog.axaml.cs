// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using JoystickGremlin.Interop.HidHide;
using JoystickGremlin.Interop.VJoy;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Combined prerequisites warning dialog shown at startup when vJoy or HidHide checks fail.
/// Sections are displayed only for failing prerequisites.
/// </summary>
public partial class PrerequisitesWarningDialog : Window
{
    /// <summary>Required by the Avalonia XAML runtime loader.</summary>
    public PrerequisitesWarningDialog() => InitializeComponent();

    /// <summary>
    /// Initializes the dialog with the results of both prerequisite checks.
    /// </summary>
    /// <param name="vjoyResult">vJoy check result — pass <c>null</c> to hide the vJoy section.</param>
    /// <param name="hidHideResult">HidHide check result — pass <c>null</c> to hide the HidHide section.</param>
    public PrerequisitesWarningDialog(
        VJoyPrerequisiteResult? vjoyResult,
        HidHidePrerequisiteResult? hidHideResult)
        : this()
    {
        if (vjoyResult is { IsOk: false })
        {
            VJoyReasonText.Text = vjoyResult.FailureReason ?? "Unknown vJoy issue.";
        }
        else
        {
            VJoySection.IsVisible = false;
        }

        if (hidHideResult is { IsOk: false })
        {
            HidHideReasonText.Text = hidHideResult.FailureReason ?? "Unknown HidHide issue.";
        }
        else
        {
            HidHideSection.IsVisible = false;
        }
    }

    private void VJoyDownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(VJoyPrerequisiteChecker.DownloadUrl)
        {
            UseShellExecute = true,
        };
        System.Diagnostics.Process.Start(psi);
    }

    private void HidHideDownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(HidHidePrerequisiteChecker.DownloadUrl)
        {
            UseShellExecute = true,
        };
        System.Diagnostics.Process.Start(psi);
    }

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => Close();
}
