// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Code-behind for <see cref="BindingsPageView"/>.
/// Shows the input-to-action binding editor for connected devices.
/// </summary>
public partial class BindingsPageView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="BindingsPageView"/>.</summary>
    public BindingsPageView()
    {
        InitializeComponent();
    }
}
