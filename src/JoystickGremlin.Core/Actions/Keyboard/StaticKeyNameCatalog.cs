// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions.Keyboard;

/// <summary>
/// Default <see cref="IKeyNameCatalog"/> used when no platform catalog is registered.
/// Exposes a minimal set of commonly used key names — arrow keys, modifiers, the alphabet,
/// digits, and a few navigation keys — so that the binding editor remains usable even when
/// running with <see cref="NullKeyboardSimulator"/> (tests / non-Windows builds).
/// </summary>
public sealed class StaticKeyNameCatalog : IKeyNameCatalog
{
    private static readonly string[] Keys = BuildKeys();

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableKeys => Keys;

    private static string[] BuildKeys()
    {
        var keys = new List<string>(capacity: 64)
        {
            // Arrow keys
            "Up", "Down", "Left", "Right",
            // Modifiers
            "LShift", "RShift", "LControl", "RControl", "LAlt", "RAlt", "LWin", "RWin",
            // Navigation / editing
            "Space", "Enter", "Tab", "Escape", "Backspace",
            "Insert", "Delete", "Home", "End", "PageUp", "PageDown",
        };

        for (var c = 'A'; c <= 'Z'; c++)
            keys.Add(c.ToString());

        for (var d = 0; d <= 9; d++)
            keys.Add($"D{d}");

        for (var f = 1; f <= 12; f++)
            keys.Add($"F{f}");

        return keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
