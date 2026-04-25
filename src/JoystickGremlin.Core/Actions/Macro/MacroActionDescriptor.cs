// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions.Keyboard;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.Macro;

/// <summary>
/// Descriptor for the macro action.
/// On activation (button press), plays a sequence of key press/release events.
/// <para>
/// Configuration keys:
/// <list type="bullet">
///   <item><c>keys</c> — comma-separated key names to press then release (e.g. <c>"LControl,C"</c>).</item>
///   <item><c>onPress</c> — bool (default <c>true</c>); if false the macro fires on button release instead.</item>
/// </list>
/// </para>
/// </summary>
public sealed class MacroActionDescriptor : IActionDescriptor
{
    private readonly IKeyboardSimulator _keyboard;
    private readonly ILogger<MacroActionDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "macro";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "Macro";

    /// <summary>
    /// Initializes a new instance of <see cref="MacroActionDescriptor"/>.
    /// </summary>
    public MacroActionDescriptor(IKeyboardSimulator keyboard, ILogger<MacroActionDescriptor> logger)
    {
        _keyboard = keyboard;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var keys = configuration?["keys"]?.GetValue<string>() ?? string.Empty;
        var onPress = configuration?["onPress"]?.GetValue<bool>() ?? true;
        var keyList = keys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new MacroFunctor(_keyboard, keyList, onPress, _logger);
    }

    private sealed class MacroFunctor : IActionFunctor
    {
        private readonly IKeyboardSimulator _keyboard;
        private readonly string[] _keys;
        private readonly bool _onPress;
        private readonly ILogger _logger;

        internal MacroFunctor(IKeyboardSimulator keyboard, string[] keys, bool onPress, ILogger logger)
        {
            _keyboard = keyboard;
            _keys = keys;
            _onPress = onPress;
            _logger = logger;
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            var isPress = inputEvent.Value >= 0.5;

            // Only execute when the button state matches the trigger condition.
            if (isPress != _onPress) return Task.CompletedTask;
            if (_keys.Length == 0) return Task.CompletedTask;

            _logger.LogTrace("Macro: pressing {Keys}", string.Join(", ", _keys));

            // Press all keys in order, then release in reverse.
            foreach (var key in _keys)
                _keyboard.KeyDown(key);

            foreach (var key in _keys.Reverse())
                _keyboard.KeyUp(key);

            return Task.CompletedTask;
        }
    }
}
