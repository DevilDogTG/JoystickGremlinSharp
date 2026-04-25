// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Represents a complete Joystick Gremlin profile containing modes and input bindings.
/// </summary>
public sealed class Profile
{
    /// <summary>Gets the unique identifier for this profile.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets or sets the display name of the profile.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets the list of modes defined in this profile.</summary>
    public List<Mode> Modes { get; init; } = [];
}

/// <summary>
/// Represents a named mode within a profile. Each mode holds a set of input bindings.
/// </summary>
public sealed class Mode
{
    /// <summary>Gets or sets the mode name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent mode name, or null for a root mode.</summary>
    public string? ParentModeName { get; set; }

    /// <summary>Gets the input bindings active in this mode.</summary>
    public List<InputBinding> Bindings { get; init; } = [];
}

/// <summary>
/// Represents the binding of a physical input to one or more actions within a mode.
/// </summary>
public sealed class InputBinding
{
    /// <summary>Gets or sets the GUID of the source physical device.</summary>
    public Guid DeviceGuid { get; set; }

    /// <summary>Gets or sets the input type (axis, button, hat).</summary>
    public Devices.InputType InputType { get; set; }

    /// <summary>Gets or sets the input identifier (axis/button/hat index).</summary>
    public int Identifier { get; set; }

    /// <summary>Gets the ordered list of actions bound to this input.</summary>
    public List<BoundAction> Actions { get; init; } = [];
}

/// <summary>
/// Represents a single action instance bound to an input with its configured data.
/// </summary>
public sealed class BoundAction
{
    /// <summary>Gets or sets the action tag identifying the action type.</summary>
    public string ActionTag { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized action configuration as a JSON object.</summary>
    public System.Text.Json.Nodes.JsonObject? Configuration { get; set; }
}
