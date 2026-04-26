// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.Keyboard;

/// <summary>
/// Descriptor for the "map-to-keyboard" action.
/// Maps a joystick button input to one or more keyboard keys using a hold-style semantic:
/// keys are pressed down while the button is held and released when the button is released.
/// </summary>
/// <remarks>
/// <para>Configuration keys:</para>
/// <list type="bullet">
///   <item><c>keys</c> — comma-separated key names (e.g. <c>"LShift,A"</c>). Modifiers first by convention.</item>
///   <item><c>behavior</c> — one of <c>"Hold"</c> (default), <c>"Toggle"</c>, <c>"PressOnly"</c>, <c>"ReleaseOnly"</c>.</item>
/// </list>
/// </remarks>
public sealed class MapToKeyboardActionDescriptor : IActionDescriptor
{
    private readonly IKeyboardSimulator _keyboard;
    private readonly ILogger<MapToKeyboardActionDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "map-to-keyboard";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "Map to Keyboard";

    /// <summary>
    /// Enumerates the hold behaviors supported by this action.
    /// </summary>
    public enum KeyBehavior
    {
        /// <summary>Keys are held while the physical button is held; released when the button releases.</summary>
        Hold,
        /// <summary>Alternates between all-keys-down and all-keys-up on each press edge.</summary>
        Toggle,
        /// <summary>Presses and immediately releases all keys on the press edge.</summary>
        PressOnly,
        /// <summary>Presses and immediately releases all keys on the release edge.</summary>
        ReleaseOnly,
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MapToKeyboardActionDescriptor"/>.
    /// </summary>
    public MapToKeyboardActionDescriptor(IKeyboardSimulator keyboard, ILogger<MapToKeyboardActionDescriptor> logger)
    {
        _keyboard = keyboard;
        _logger   = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var rawKeys  = configuration?["keys"]?.GetValue<string>() ?? string.Empty;
        var rawBehavior = configuration?["behavior"]?.GetValue<string>() ?? "Hold";

        var keys = rawKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!Enum.TryParse<KeyBehavior>(rawBehavior, ignoreCase: true, out var behavior))
            behavior = KeyBehavior.Hold;

        return new MapToKeyboardFunctor(_keyboard, keys, behavior, _logger);
    }

    // ─── Functor ─────────────────────────────────────────────────────────────

    private sealed class MapToKeyboardFunctor : IActionFunctor
    {
        private readonly IKeyboardSimulator _keyboard;
        private readonly string[] _keys;
        private readonly KeyBehavior _behavior;
        private readonly ILogger _logger;
        private readonly object _toggleLock = new();
        private bool _toggleState;

        internal MapToKeyboardFunctor(
            IKeyboardSimulator keyboard,
            string[] keys,
            KeyBehavior behavior,
            ILogger logger)
        {
            _keyboard = keyboard;
            _keys     = keys;
            _behavior = behavior;
            _logger   = logger;
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            if (_keys.Length == 0) return Task.CompletedTask;

            var isPress = inputEvent.Value >= 0.5;

            _logger.LogTrace(
                "MapToKeyboard {Behavior}: press={Press} keys={Keys}",
                _behavior, isPress, string.Join(",", _keys));

            switch (_behavior)
            {
                case KeyBehavior.Hold:
                    // Keep keys down while button is held; release when button releases.
                    if (isPress)
                        PressAll();
                    else
                        ReleaseAll();
                    break;

                case KeyBehavior.Toggle:
                    // On press edge only: alternate between press-all and release-all.
                    // Lock guards the read-modify-write so concurrent dispatches cannot
                    // corrupt _toggleState (e.g. two rapid press events via Task.Run).
                    if (isPress)
                    {
                        lock (_toggleLock)
                        {
                            if (_toggleState)
                                ReleaseAll();
                            else
                                PressAll();
                            _toggleState = !_toggleState;
                        }
                    }
                    break;

                case KeyBehavior.PressOnly:
                    // Press then immediately release on press edge.
                    if (isPress)
                    {
                        PressAll();
                        ReleaseAll();
                    }
                    break;

                case KeyBehavior.ReleaseOnly:
                    // Press then immediately release on release edge.
                    if (!isPress)
                    {
                        PressAll();
                        ReleaseAll();
                    }
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>Presses all keys in list order (modifiers first by convention).</summary>
        private void PressAll()
        {
            foreach (var key in _keys)
                _keyboard.KeyDown(key);
        }

        /// <summary>Releases all keys in reverse order (normal keys before modifiers).</summary>
        private void ReleaseAll()
        {
            foreach (var key in _keys.Reverse())
                _keyboard.KeyUp(key);
        }
    }
}
