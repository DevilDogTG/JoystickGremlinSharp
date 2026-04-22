// SPDX-License-Identifier: GPL-3.0-only

using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private string _title = "Joystick Gremlin";

    /// <summary>
    /// Gets or sets the window title.
    /// </summary>
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }
}
