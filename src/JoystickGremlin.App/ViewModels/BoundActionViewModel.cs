// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Actions.ChangeMode;
using JoystickGremlin.Core.Actions.Keyboard;
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

    /// <summary>Gets whether this action is a map-to-keyboard action.</summary>
    public bool IsMapToKeyboard => ActionTag == MapToKeyboardActionDescriptor.ActionTag;

    /// <summary>Gets whether this action maps multiple buttons to a vJoy hat.</summary>
    public bool IsButtonsToHat => ActionTag == ButtonsToHatDescriptor.ActionTag;

    /// <summary>Gets whether this action maps multiple buttons to vJoy axes.</summary>
    public bool IsButtonsToAxes => ActionTag == ButtonsToAxesDescriptor.ActionTag;

    /// <summary>
    /// Gets the name of the ancestor mode this action is inherited from,
    /// or <c>null</c> if the action is defined directly in the editing mode.
    /// </summary>
    public string? InheritedFromMode { get; }

    /// <summary>Gets whether this action is inherited from a parent mode.</summary>
    public bool IsInherited => InheritedFromMode is not null;

    /// <summary>
    /// Initializes a new instance of <see cref="BoundActionViewModel"/>.
    /// </summary>
    /// <param name="model">The underlying domain bound action.</param>
    /// <param name="registry">Registry used to resolve the display name.</param>
    /// <param name="inheritedFromMode">
    /// Name of the ancestor mode this action comes from, or <c>null</c> if it is owned by the editing mode.
    /// </param>
    public BoundActionViewModel(BoundAction model, IActionRegistry registry, string? inheritedFromMode = null)
    {
        Model = model;
        InheritedFromMode = inheritedFromMode;
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
                BuildVJoyButtonSummary(cfg),
            VJoyHatDescriptor.ActionTag =>
                $"Device {cfg["vjoyId"]?.GetValue<int>() ?? 1}, Hat {cfg["hatIndex"]?.GetValue<int>() ?? 1}",
            ButtonsToHatDescriptor.ActionTag =>
                BuildButtonsToHatSummary(cfg),
            ButtonsToAxesDescriptor.ActionTag =>
                BuildButtonsToAxesSummary(cfg),
            ChangeModeActionDescriptor.ActionTag =>
                $"→ {cfg["targetMode"]?.GetValue<string>() ?? "(unset)"}",
            MacroActionDescriptor.ActionTag =>
                cfg["keys"]?.GetValue<string>() is { Length: > 0 } k ? k : "(no keys)",
            MapToKeyboardActionDescriptor.ActionTag =>
                BuildMapToKeyboardSummary(cfg),
            _ => "(default config)",
        };
    }

    private static string BuildVJoyButtonSummary(JsonObject? cfg)
    {
        if (cfg is null) return "(default config)";
        var device = cfg["vjoyId"]?.GetValue<int>() ?? 1;
        var button = cfg["buttonIndex"]?.GetValue<int>() ?? 1;
        var threshold = cfg["threshold"]?.GetValue<double>() ?? 0.5;
        var thresholdPart = Math.Abs(threshold - 0.5) > 0.001
            ? $", Threshold {threshold:P0}"
            : string.Empty;
        return $"Device {device}, Button {button}{thresholdPart}";
    }

    private static string BuildMapToKeyboardSummary(JsonObject? cfg)
    {
        if (cfg is null) return "(no keys)";
        var keys     = cfg["keys"]?.GetValue<string>() ?? string.Empty;
        var behavior = cfg["behavior"]?.GetValue<string>() ?? "Hold";
        var keysPart = keys.Length > 0 ? keys : "(no keys)";
        return behavior == "Hold" ? keysPart : $"{keysPart} [{behavior}]";
    }

    private static string BuildButtonsToHatSummary(JsonObject? cfg)
    {
        if (cfg is null) return "(unconfigured)";

        var vjoyId = cfg["vjoyId"]?.GetValue<int>() ?? 1;
        var hatIndex = cfg["hatIndex"]?.GetValue<int>() ?? 1;
        var up = cfg["upButtonId"]?.GetValue<int>() ?? 0;
        var down = cfg["downButtonId"]?.GetValue<int>() ?? 0;
        var left = cfg["leftButtonId"]?.GetValue<int>() ?? 0;
        var right = cfg["rightButtonId"]?.GetValue<int>() ?? 0;

        return $"U{up} D{down} L{left} R{right} → vJoy {vjoyId} Hat {hatIndex}";
    }

    private static string BuildButtonsToAxesSummary(JsonObject? cfg)
    {
        if (cfg is null) return "(unconfigured)";

        var vjoyId = cfg["vjoyId"]?.GetValue<int>() ?? 1;
        var xAxisIndex = cfg["xAxisIndex"]?.GetValue<int>() ?? 1;
        var yAxisIndex = cfg["yAxisIndex"]?.GetValue<int>() ?? 2;
        var up = cfg["upButtonId"]?.GetValue<int>() ?? 0;
        var down = cfg["downButtonId"]?.GetValue<int>() ?? 0;
        var left = cfg["leftButtonId"]?.GetValue<int>() ?? 0;
        var right = cfg["rightButtonId"]?.GetValue<int>() ?? 0;

        return $"U{up} D{down} L{left} R{right} → vJoy {vjoyId} X{xAxisIndex}/Y{yAxisIndex}";
    }
}
