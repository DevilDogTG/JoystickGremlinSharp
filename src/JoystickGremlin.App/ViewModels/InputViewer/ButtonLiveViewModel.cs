// SPDX-License-Identifier: GPL-3.0-only

using ReactiveUI;

namespace JoystickGremlin.App.ViewModels.InputViewer;

/// <summary>
/// Represents the live press state of a single joystick button.
/// </summary>
public sealed class ButtonLiveViewModel : ReactiveObject
{
    private bool _isPressed;

    /// <summary>Gets the 1-based button index.</summary>
    public int ButtonIndex { get; }

    /// <summary>Gets the display label (e.g. "1").</summary>
    public string Label => ButtonIndex.ToString();

    /// <summary>Gets or sets a value indicating whether the button is currently pressed.</summary>
    public bool IsPressed
    {
        get => _isPressed;
        set => this.RaiseAndSetIfChanged(ref _isPressed, value);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ButtonLiveViewModel"/>.
    /// </summary>
    public ButtonLiveViewModel(int buttonIndex)
    {
        ButtonIndex = buttonIndex;
    }
}
