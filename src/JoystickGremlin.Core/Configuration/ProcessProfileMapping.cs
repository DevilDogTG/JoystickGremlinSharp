// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// Maps a Windows executable to a JoystickGremlin profile.
/// When the mapped executable becomes the foreground window, the profile is loaded automatically.
/// The executable is matched either by file name or by full path — see <see cref="MatchType"/>.
/// </summary>
public sealed class ProcessProfileMapping
{
    // IMPORTANT — STJ-deserialization invariant:
    // This property MUST NOT have a C# initializer. System.Text.Json does not write to
    // properties absent from the JSON document, so an initializer would override the
    // legacy-compatible default. Keeping it unset means absent "MatchType" → enum value 0
    // → ExecutablePath, which is the only state where pre-existing settings.json files
    // (containing a bare "ExecutablePath" field) continue to resolve correctly.
    // Locked by ProcessProfileMappingCompatTests. Do not "tidy up" by adding `= ...`.

    /// <summary>
    /// Gets or sets how the foreground executable is matched against this mapping.
    /// Defaults to <see cref="ProcessMatchType.ExecutablePath"/> via the enum's underlying
    /// value 0 — see the IMPORTANT note above. The process picker sets
    /// <see cref="ProcessMatchType.ExecutableName"/> explicitly when used.
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
