// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// Maps a Windows executable (by exact path or regex pattern) to a JoystickGremlin profile.
/// When the mapped executable becomes the foreground window, the profile is loaded automatically.
/// </summary>
public sealed class ProcessProfileMapping
{
    /// <summary>
    /// Gets or sets the executable path or regex pattern to match.
    /// Examples: <c>"C:/Games/DCS.exe"</c> (exact) or <c>".*DCS.*"</c> (regex).
    /// Matching is case-insensitive. Exact match is tried before regex.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the absolute path to the profile JSON file to load.</summary>
    public string ProfilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this entry is active.
    /// Disabled entries are ignored during matching.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the event pipeline should be started automatically
    /// when this mapping activates.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the event pipeline should remain active
    /// when the user switches to a different window (focus loss).
    /// When <c>false</c>, the pipeline is stopped when no mapped process is in the foreground.
    /// </summary>
    public bool RemainActiveOnFocusLoss { get; set; } = false;
}
