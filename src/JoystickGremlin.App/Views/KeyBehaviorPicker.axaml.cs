// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;

namespace JoystickGremlin.App.Views;

/// <summary>
/// Code-behind for <see cref="KeyBehaviorPicker"/>.
/// Shared map-to-keyboard behavior picker: dropdown with per-option descriptions and
/// a caption for the current selection. Expects a
/// <see cref="ViewModels.BindingsPageViewModel"/> as its DataContext.
/// </summary>
public partial class KeyBehaviorPicker : UserControl
{
    /// <summary>Initializes a new instance of <see cref="KeyBehaviorPicker"/>.</summary>
    public KeyBehaviorPicker()
    {
        InitializeComponent();
    }
}
