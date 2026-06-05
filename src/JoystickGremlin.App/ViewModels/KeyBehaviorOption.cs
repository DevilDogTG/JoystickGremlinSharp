// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Actions.Keyboard;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// UI option for one map-to-keyboard behavior: the persisted enum name, a display
/// label, and a plain-language description shown in the behavior picker so users
/// can choose without prior knowledge of the semantics.
/// </summary>
/// <param name="Value">
/// Enum name persisted in the action configuration (matches
/// <see cref="MapToKeyboardActionDescriptor.KeyBehavior"/>, e.g. <c>"PressOnly"</c>).
/// </param>
/// <param name="Label">Human-readable label (e.g. <c>"Press Only"</c>).</param>
/// <param name="Description">Plain-language guidance on when to choose this behavior.</param>
public sealed record KeyBehaviorOption(string Value, string Label, string Description)
{
    /// <summary>
    /// All selectable behaviors, in the order they appear in the picker. One entry per
    /// <see cref="MapToKeyboardActionDescriptor.KeyBehavior"/> value.
    /// </summary>
    public static IReadOnlyList<KeyBehaviorOption> All { get; } =
    [
        new(nameof(MapToKeyboardActionDescriptor.KeyBehavior.Hold), "Hold",
            "Keys stay pressed while the button is held, released when you let go. " +
            "Standard choice for normal (momentary) buttons."),
        new(nameof(MapToKeyboardActionDescriptor.KeyBehavior.Toggle), "Toggle",
            "Each press flips the keys between pressed and released. " +
            "Latches a state from a momentary button (e.g. push-to-talk → mic on/off)."),
        new(nameof(MapToKeyboardActionDescriptor.KeyBehavior.PressOnly), "Press Only",
            "Sends one quick key tap when the button is pressed; nothing on release. " +
            "Use for 2-position switches so a key isn't held down forever."),
        new(nameof(MapToKeyboardActionDescriptor.KeyBehavior.ReleaseOnly), "Release Only",
            "Sends one quick key tap when the button is released. " +
            "Pair with Press Only on the same input to send different keys on switch flip-up vs flip-down."),
    ];
}
