// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Code-behind for <see cref="DevicesPageView"/>.
/// Shows connected physical input devices and their input-binding table.
/// </summary>
public partial class DevicesPageView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="DevicesPageView"/>.</summary>
    public DevicesPageView()
    {
        InitializeComponent();
    }
}
