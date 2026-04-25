// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions.Keyboard;

/// <summary>
/// No-operation implementation of <see cref="IKeyboardSimulator"/>.
/// Used in unit tests and as a safe default when no platform implementation is available.
/// </summary>
public sealed class NullKeyboardSimulator : IKeyboardSimulator
{
    /// <inheritdoc/>
    public void KeyDown(string keyName) { }

    /// <inheritdoc/>
    public void KeyUp(string keyName) { }
}
