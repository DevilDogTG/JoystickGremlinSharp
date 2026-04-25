// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace JoystickGremlin.App.Views.InputViewer;

public partial class InputViewerPageView : UserControl
{
    public InputViewerPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
