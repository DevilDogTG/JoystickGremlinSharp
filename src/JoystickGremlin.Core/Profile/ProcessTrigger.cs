// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// A process-based auto-load trigger embedded inside a <see cref="Profile"/>.
/// When the matched executable becomes the foreground window, the owning profile
/// is loaded automatically.
/// </summary>
/// <remarks>
/// Triggers live inside their target profile (<see cref="Profile.AutoLoadTriggers"/>),
/// so sharing or deleting a profile carries its triggers with it. There is no
/// global mapping list — see release notes for v11.0 for the breaking change.
/// </remarks>
public sealed class ProcessTrigger
{
    /// <summary>
    /// Gets or sets how the foreground executable is matched against this trigger.
    /// Defaults to <see cref="ProcessMatchType.ExecutablePath"/> (enum value 0).
    /// </summary>
    public ProcessMatchType MatchType { get; set; }

    /// <summary>
    /// Gets or sets the executable file name to match when <see cref="MatchType"/> is
    /// <see cref="ProcessMatchType.ExecutableName"/>. Example: <c>"DCS.exe"</c>. Case-insensitive.
    /// </summary>
    public string ExecutableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full executable path. Used for matching when <see cref="MatchType"/> is
    /// <see cref="ProcessMatchType.ExecutablePath"/>; in name-match mode it is retained for display
    /// (the path captured when the process was picked). Example: <c>"C:/Games/DCS.exe"</c>.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this trigger is active.
    /// Disabled triggers are ignored during matching.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the event pipeline should be started automatically
    /// when this trigger activates.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the event pipeline should remain active
    /// when the user switches to a different window (focus loss).
    /// When <c>false</c>, the pipeline is stopped when no triggered process is in the foreground.
    /// </summary>
    public bool RemainActiveOnFocusLoss { get; set; } = false;
}
