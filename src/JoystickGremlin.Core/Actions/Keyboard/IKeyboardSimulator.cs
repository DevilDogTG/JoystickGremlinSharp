// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions.Keyboard;

/// <summary>
/// Abstracts platform-specific keyboard simulation used by the macro action functor.
/// Register a platform implementation (e.g. via SendInput on Windows) in the Interop layer,
/// or use <see cref="NullKeyboardSimulator"/> in tests and non-Windows builds.
/// </summary>
public interface IKeyboardSimulator
{
    /// <summary>
    /// Sends a key press (down) event for the specified virtual key name.
    /// </summary>
    /// <param name="keyName">Platform-independent key name (e.g. "A", "LControl", "Space").</param>
    void KeyDown(string keyName);

    /// <summary>
    /// Sends a key release (up) event for the specified virtual key name.
    /// </summary>
    /// <param name="keyName">Platform-independent key name (e.g. "A", "LControl", "Space").</param>
    void KeyUp(string keyName);
}
