// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Code-behind for <see cref="ControllerSetupPageView"/>.
/// Hosts the merged device, live input, and binding editor experience.
/// </summary>
public partial class ControllerSetupPageView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="ControllerSetupPageView"/>.</summary>
    public ControllerSetupPageView()
    {
        InitializeComponent();
    }
}
