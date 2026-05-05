// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Modal dialog shown when the active profile requires EmuWheel but the process is not
/// running with administrator privileges.
/// </summary>
/// <remarks>
/// Returns <c>true</c> (via <see cref="Window.Close(object?)"/>) when the user chooses to
/// restart as administrator, and <c>false</c> when the user chooses to continue without
/// EmuWheel identity spoofing.
/// </remarks>
public partial class EmuWheelAdminDialog : Window
{
    /// <summary>Required by the Avalonia XAML runtime loader.</summary>
    public EmuWheelAdminDialog() => InitializeComponent();

    private void RestartButton_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => Close(false);
}
