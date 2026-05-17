// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Code-behind for the HidHide integration settings page.
/// </summary>
public partial class HidHidePageView : UserControl
{
    /// <summary>
    /// Initializes a new instance of <see cref="HidHidePageView"/>.
    /// </summary>
    public HidHidePageView()
    {
        InitializeComponent();
    }

    private void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
            OpenUrl(url);
    }

    private void RemoveStale_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ViewModels.HidHideDeviceRowViewModel row })
            row.RequestRemove();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* best-effort */ }
    }
}
