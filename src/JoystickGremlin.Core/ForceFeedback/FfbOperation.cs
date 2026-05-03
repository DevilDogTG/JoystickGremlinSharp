// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Specifies the operation to perform on a force feedback effect.
/// </summary>
public enum FfbOperation
{
    /// <summary>Start the effect, allowing looping.</summary>
    Start = 1,

    /// <summary>Start the effect as the only playing effect (stops others).</summary>
    StartSolo = 2,

    /// <summary>Stop the effect.</summary>
    Stop = 3,
}
