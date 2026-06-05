// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ProcessMonitor;

/// <summary>
/// A process-based auto-load trigger stored in the global trigger list
/// (<c>AppSettings.AutoLoadTriggers</c>). When the matched executable becomes the
/// foreground window, the profile referenced by <see cref="ProfilePath"/> is loaded
/// automatically.
/// </summary>
/// <remarks>
/// Since v12.1 triggers live in <c>settings.json</c> rather than inside each profile
/// (the v11.0 model). Triggers found embedded in profile files are lifted into the
/// global list by <c>AutoLoadTriggerMigrator</c>. Triggers are evaluated in list order;
/// the first enabled match wins.
/// </remarks>
public sealed class AutoLoadTrigger
{
    /// <summary>
    /// Gets or sets the absolute path of the profile JSON file to load when this
    /// trigger activates. A trigger whose profile file no longer exists never matches.
    /// </summary>
    public string ProfilePath { get; set; } = string.Empty;

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
