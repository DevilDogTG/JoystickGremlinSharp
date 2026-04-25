// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Represents a single axis, button, or hat row in the device input-binding table.
/// </summary>
public sealed class InputEntryViewModel : ViewModelBase
{
    private bool _isActive;
    private string _boundActions = "(none)";

    /// <summary>
    /// Initializes a new instance of <see cref="InputEntryViewModel"/>.
    /// </summary>
    /// <param name="inputType">The type of the input.</param>
    /// <param name="index">1-based index of the input on the device.</param>
    public InputEntryViewModel(InputType inputType, int index)
    {
        InputType = inputType;
        Index = index;
    }

    /// <summary>Gets the input type (Axis, Button, Hat).</summary>
    public InputType InputType { get; }

    /// <summary>Gets the 1-based input index.</summary>
    public int Index { get; }

    /// <summary>Gets a human-readable label such as "Axis 1" or "Button 3".</summary>
    public string Label => InputType switch
    {
        InputType.JoystickAxis   => $"Axis {Index}",
        InputType.JoystickButton => $"Button {Index}",
        InputType.JoystickHat    => $"Hat {Index}",
        _                        => $"{InputType} {Index}"
    };

    /// <summary>Gets or sets a comma-separated list of bound action names, or "(none)".</summary>
    public string BoundActions
    {
        get => _boundActions;
        set => this.RaiseAndSetIfChanged(ref _boundActions, value);
    }

    /// <summary>Gets or sets a value indicating whether this input was recently activated (used for live highlighting).</summary>
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }
}
