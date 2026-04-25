// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Represents a single axis, button, or hat switch on a physical device for display in the bindings list.
/// </summary>
public sealed class InputDescriptorViewModel : ViewModelBase
{
    /// <summary>Gets the input type.</summary>
    public InputType InputType { get; }

    /// <summary>Gets the 1-based identifier (axis/button/hat index).</summary>
    public int Identifier { get; }

    /// <summary>Gets the human-readable label for this input (e.g. "Axis 1", "Button 3").</summary>
    public string DisplayName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="InputDescriptorViewModel"/>.
    /// </summary>
    public InputDescriptorViewModel(InputType inputType, int identifier)
    {
        InputType = inputType;
        Identifier = identifier;
        DisplayName = inputType switch
        {
            InputType.JoystickAxis   => $"Axis {identifier}",
            InputType.JoystickButton => $"Button {identifier}",
            InputType.JoystickHat    => $"Hat {identifier}",
            _                        => $"{inputType} {identifier}",
        };
    }
}
