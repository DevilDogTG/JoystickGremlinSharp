// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Determines how a <see cref="ProcessTrigger"/> is matched against the
/// foreground process's executable path.
/// </summary>
public enum ProcessMatchType
{
    /// <summary>
    /// Match on the full executable path (case-insensitive, path-separator agnostic).
    /// Value <c>0</c> is the default for new triggers and for triggers whose JSON
    /// omits the <c>MatchType</c> field.
    /// </summary>
    ExecutablePath = 0,

    /// <summary>
    /// Match on the executable file name only (e.g. <c>DCS.exe</c>), case-insensitive.
    /// Survives reinstalls and different install locations. Set when a process is chosen via the picker.
    /// </summary>
    ExecutableName = 1,
}
