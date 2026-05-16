// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions;

/// <summary>
/// Optional capability marker for <see cref="IActionDescriptor"/> implementations that hold
/// stateful information shared across their functors (e.g. multi-button D-pad state).
/// The event pipeline calls <see cref="ResetState"/> when the active profile changes or the
/// pipeline stops, to prevent stale logical-input state from bleeding across profile swaps.
/// </summary>
public interface IResettableActionDescriptor
{
    /// <summary>
    /// Clears any descriptor-level state shared across functors.
    /// Implementations must be safe to call concurrently with running functors.
    /// </summary>
    void ResetState();
}
