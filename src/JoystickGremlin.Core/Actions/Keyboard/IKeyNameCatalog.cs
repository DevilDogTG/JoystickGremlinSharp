// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions.Keyboard;

/// <summary>
/// Provides the list of keyboard key names known to the active <see cref="IKeyboardSimulator"/>.
/// Used by the binding-editor UI to populate a searchable key picker so users do not have to
/// guess the exact spelling of key names (e.g. "Up", "LControl", "OemSemicolon").
/// </summary>
/// <remarks>
/// Implementations should return a stable, distinct, case-insensitive list ordered for display.
/// The Core layer registers <see cref="StaticKeyNameCatalog"/> by default; the Interop layer
/// overrides it with a full catalog derived from the platform key table.
/// </remarks>
public interface IKeyNameCatalog
{
    /// <summary>
    /// Gets the full list of available key names, distinct and sorted for display.
    /// </summary>
    IReadOnlyList<string> AvailableKeys { get; }
}
