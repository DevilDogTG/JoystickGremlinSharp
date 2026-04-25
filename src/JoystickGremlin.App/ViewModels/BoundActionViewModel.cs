// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Actions.ChangeMode;
using JoystickGremlin.Core.Actions.Macro;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Profile;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Wraps a <see cref="BoundAction"/> domain model for display in the bindings editor list.
/// </summary>
public sealed class BoundActionViewModel : ViewModelBase
{
    /// <summary>Gets the underlying domain model.</summary>
    public BoundAction Model { get; }

    /// <summary>Gets the action tag (e.g. "vjoy-axis", "macro").</summary>
    public string ActionTag => Model.ActionTag;

    /// <summary>Gets the human-readable action name resolved from the registry, or the tag if not found.</summary>
    public string ActionName { get; }

    /// <summary>Gets a one-line summary of the action's current configuration.</summary>
    public string ConfigSummary { get; }

    /// <summary>Gets whether this action is a vJoy axis action.</summary>
    public bool IsVJoyAxis => ActionTag == VJoyAxisDescriptor.ActionTag;

    /// <summary>Gets whether this action is a vJoy button action.</summary>
    public bool IsVJoyButton => ActionTag == VJoyButtonDescriptor.ActionTag;

    /// <summary>Gets whether this action is a vJoy hat action.</summary>
    public bool IsVJoyHat => ActionTag == VJoyHatDescriptor.ActionTag;

    /// <summary>Gets whether this action is a change-mode action.</summary>
    public bool IsChangeMode => ActionTag == ChangeModeActionDescriptor.ActionTag;

    /// <summary>Gets whether this action is a macro action.</summary>
    public bool IsMacro => ActionTag == MacroActionDescriptor.ActionTag;

    /// <summary>
    /// Initializes a new instance of <see cref="BoundActionViewModel"/>.
    /// </summary>
    /// <param name="model">The underlying domain bound action.</param>
    /// <param name="registry">Registry used to resolve the display name.</param>
    public BoundActionViewModel(BoundAction model, IActionRegistry registry)
    {
        Model = model;
        var descriptor = registry.Resolve(model.ActionTag);
        ActionName = descriptor?.Name ?? model.ActionTag;
        ConfigSummary = BuildSummary(model);
    }

    private static string BuildSummary(BoundAction model)
    {
        var cfg = model.Configuration;
        if (cfg is null) return "(default config)";

        return model.ActionTag switch
        {
            VJoyAxisDescriptor.ActionTag =>
                $"Device {cfg["vjoyId"]?.GetValue<int>() ?? 1}, Axis {cfg["axisIndex"]?.GetValue<int>() ?? 1}",
            VJoyButtonDescriptor.ActionTag =>
                $"Device {cfg["vjoyId"]?.GetValue<int>() ?? 1}, Button {cfg["buttonIndex"]?.GetValue<int>() ?? 1}",
            VJoyHatDescriptor.ActionTag =>
                $"Device {cfg["vjoyId"]?.GetValue<int>() ?? 1}, Hat {cfg["hatIndex"]?.GetValue<int>() ?? 1}",
            ChangeModeActionDescriptor.ActionTag =>
                $"→ {cfg["targetMode"]?.GetValue<string>() ?? "(unset)"}",
            MacroActionDescriptor.ActionTag =>
                cfg["keys"]?.GetValue<string>() is { Length: > 0 } k ? k : "(no keys)",
            _ => "(default config)",
        };
    }
}
