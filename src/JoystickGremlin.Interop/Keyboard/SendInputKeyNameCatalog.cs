// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Actions.Keyboard;

namespace JoystickGremlin.Interop.Keyboard;

/// <summary>
/// <see cref="IKeyNameCatalog"/> implementation backed by the full
/// <see cref="SendInputKeyboardSimulator"/> scan-code table. Provides the binding-editor UI
/// with every key name supported by the Windows SendInput simulator.
/// </summary>
public sealed class SendInputKeyNameCatalog : IKeyNameCatalog
{
    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableKeys { get; } =
        SendInputKeyboardSimulator.GetKeyNames().ToArray();
}
