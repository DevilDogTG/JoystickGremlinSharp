// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// Determines how a <see cref="ProcessProfileMapping"/> is matched against the
/// foreground process's executable path.
/// </summary>
public enum ProcessMatchType
{
    /// <summary>
    /// Match on the full executable path (case-insensitive, path-separator agnostic).
    /// This is value <c>0</c> so that settings persisted before the match-type concept existed
    /// — which stored a path in <see cref="ProcessProfileMapping.ExecutablePath"/> — deserialize
    /// into this mode without an explicit migration.
    /// </summary>
    ExecutablePath = 0,

    /// <summary>
    /// Match on the executable file name only (e.g. <c>DCS.exe</c>), case-insensitive.
    /// Survives reinstalls and different install locations. Set when a process is chosen via the picker.
    /// </summary>
    ExecutableName = 1,
}
