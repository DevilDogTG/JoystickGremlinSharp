// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.Keyboard;

/// <summary>
/// Descriptor for the "map-to-arrow-keys" action.
/// Maps a hat/POV input or four physical buttons to four keyboard keys
/// (Up/Down/Left/Right). Diagonals press two keys at once (true 8-way).
/// Only changed keys are pressed/released between events to avoid game-side
/// key-repeat jitter.
/// </summary>
/// <remarks>
/// <para>Configuration keys:</para>
/// <list type="bullet">
///   <item><c>upKey</c> — keyboard key name fired for the Up direction (default <c>"Up"</c>).</item>
///   <item><c>downKey</c> — keyboard key name fired for the Down direction (default <c>"Down"</c>).</item>
///   <item><c>leftKey</c> — keyboard key name fired for the Left direction (default <c>"Left"</c>).</item>
///   <item><c>rightKey</c> — keyboard key name fired for the Right direction (default <c>"Right"</c>).</item>
///   <item><c>upButtonId</c> — physical button id for Up (Buttons mode only; ignored for Hat input).</item>
///   <item><c>downButtonId</c> — physical button id for Down (Buttons mode only).</item>
///   <item><c>leftButtonId</c> — physical button id for Left (Buttons mode only).</item>
///   <item><c>rightButtonId</c> — physical button id for Right (Buttons mode only).</item>
/// </list>
/// <para>Mode is auto-detected from <see cref="InputEvent.InputType"/>: Hat events
/// resolve the angle to an 8-way direction set; Button events update a shared
/// per-config state so four bindings cooperate as a single D-pad.</para>
/// <para>Empty key names disable that direction (no key issued).</para>
/// </remarks>
public sealed class MapToArrowKeysActionDescriptor : IActionDescriptor
{
    private readonly IKeyboardSimulator _keyboard;
    private readonly ILogger<MapToArrowKeysActionDescriptor> _logger;

    private readonly Dictionary<string, ButtonsState> _sharedButtonStates = new(StringComparer.Ordinal);
    private readonly Lock _stateLock = new();

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "map-to-arrow-keys";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "Map Hat/Buttons to Arrow Keys";

    /// <summary>
    /// Initializes a new instance of <see cref="MapToArrowKeysActionDescriptor"/>.
    /// </summary>
    public MapToArrowKeysActionDescriptor(
        IKeyboardSimulator keyboard,
        ILogger<MapToArrowKeysActionDescriptor> logger)
    {
        _keyboard = keyboard;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var upKey    = configuration?["upKey"]?.GetValue<string>()    ?? "Up";
        var downKey  = configuration?["downKey"]?.GetValue<string>()  ?? "Down";
        var leftKey  = configuration?["leftKey"]?.GetValue<string>()  ?? "Left";
        var rightKey = configuration?["rightKey"]?.GetValue<string>() ?? "Right";

        var upBtn    = configuration?["upButtonId"]?.GetValue<int>()    ?? -1;
        var downBtn  = configuration?["downButtonId"]?.GetValue<int>()  ?? -1;
        var leftBtn  = configuration?["leftButtonId"]?.GetValue<int>()  ?? -1;
        var rightBtn = configuration?["rightButtonId"]?.GetValue<int>() ?? -1;

        return new MapToArrowKeysFunctor(
            _keyboard,
            upKey, downKey, leftKey, rightKey,
            upBtn, downBtn, leftBtn, rightBtn,
            this,
            _logger);
    }

    // ─── Public helpers (visible for unit tests) ─────────────────────────────

    /// <summary>
    /// Resolves a hat angle to an 8-way direction set (Up/Down/Left/Right flags).
    /// </summary>
    /// <param name="hatValue">
    /// Hat value: <c>-1</c> = center; <c>0–359</c> = degrees; <c>0–35999</c> = centidegrees.
    /// </param>
    /// <returns>The set of cardinal directions to be held for this hat position.</returns>
    public static Direction ResolveHatDirection(double hatValue)
    {
        if (hatValue < 0)
            return Direction.None;

        // Normalise centidegrees to degrees (vJoy uses centidegrees, DILL uses degrees).
        double degrees = hatValue >= 360.0 ? hatValue / 100.0 : hatValue;

        // Wrap into [0, 360) and shift so each 45° sector centers on a cardinal/diagonal.
        // Sectors: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
        degrees = ((degrees % 360.0) + 360.0) % 360.0;
        int sector = ((int)Math.Floor((degrees + 22.5) / 45.0)) % 8;

        return sector switch
        {
            0 => Direction.Up,
            1 => Direction.Up   | Direction.Right,
            2 => Direction.Right,
            3 => Direction.Down | Direction.Right,
            4 => Direction.Down,
            5 => Direction.Down | Direction.Left,
            6 => Direction.Left,
            7 => Direction.Up   | Direction.Left,
            _ => Direction.None,
        };
    }

    /// <summary>
    /// Bitfield of cardinal directions currently active.
    /// </summary>
    [Flags]
    public enum Direction
    {
        /// <summary>No direction active (center).</summary>
        None  = 0,
        /// <summary>North / Up.</summary>
        Up    = 1 << 0,
        /// <summary>South / Down.</summary>
        Down  = 1 << 1,
        /// <summary>West / Left.</summary>
        Left  = 1 << 2,
        /// <summary>East / Right.</summary>
        Right = 1 << 3,
    }

    // ─── Functor ─────────────────────────────────────────────────────────────

    private sealed class MapToArrowKeysFunctor : IActionFunctor
    {
        private readonly IKeyboardSimulator _keyboard;
        private readonly string _upKey, _downKey, _leftKey, _rightKey;
        private readonly int _upBtn, _downBtn, _leftBtn, _rightBtn;
        private readonly MapToArrowKeysActionDescriptor _descriptor;
        private readonly ILogger _logger;
        private readonly string _buttonStateKey;
        private readonly Lock _hatLock = new();
        private Direction _lastHatPressed;

        internal MapToArrowKeysFunctor(
            IKeyboardSimulator keyboard,
            string upKey, string downKey, string leftKey, string rightKey,
            int upBtn, int downBtn, int leftBtn, int rightBtn,
            MapToArrowKeysActionDescriptor descriptor,
            ILogger logger)
        {
            _keyboard = keyboard;
            _upKey = upKey; _downKey = downKey; _leftKey = leftKey; _rightKey = rightKey;
            _upBtn = upBtn; _downBtn = downBtn; _leftBtn = leftBtn; _rightBtn = rightBtn;
            _descriptor = descriptor;
            _logger = logger;
            // Keys identify the output target uniquely.
            _buttonStateKey = $"{upKey}|{downKey}|{leftKey}|{rightKey}";
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                switch (inputEvent.InputType)
                {
                    case InputType.JoystickHat:
                        HandleHat(inputEvent.Value);
                        break;

                    case InputType.JoystickButton:
                        HandleButton(inputEvent.Identifier, inputEvent.Value >= 0.5);
                        break;

                    default:
                        _logger.LogTrace(
                            "MapToArrowKeys: ignoring unsupported input type {InputType}",
                            inputEvent.InputType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MapToArrowKeys action failed");
            }

            return Task.CompletedTask;
        }

        private void HandleHat(double value)
        {
            var newDirs = ResolveHatDirection(value);
            Direction prev;
            lock (_hatLock)
            {
                prev = _lastHatPressed;
                _lastHatPressed = newDirs;
            }
            EmitDiff(prev, newDirs);
        }

        private void HandleButton(int identifier, bool pressed)
        {
            Direction? which =
                identifier == _upBtn    ? Direction.Up    :
                identifier == _downBtn  ? Direction.Down  :
                identifier == _leftBtn  ? Direction.Left  :
                identifier == _rightBtn ? Direction.Right :
                null;

            if (which is null)
            {
                _logger.LogTrace(
                    "MapToArrowKeys (Buttons): ignoring identifier {Identifier} (not a configured button)",
                    identifier);
                return;
            }

            Direction prev, next;
            lock (_descriptor._stateLock)
            {
                if (!_descriptor._sharedButtonStates.TryGetValue(_buttonStateKey, out var state))
                {
                    state = new ButtonsState();
                    _descriptor._sharedButtonStates[_buttonStateKey] = state;
                }

                prev = state.Pressed;
                if (pressed)
                    state.Pressed |= which.Value;
                else
                    state.Pressed &= ~which.Value;
                next = state.Pressed;
            }
            EmitDiff(prev, next);
        }

        private void EmitDiff(Direction prev, Direction next)
        {
            if (prev == next) return;

            var toRelease = prev & ~next;
            var toPress   = next & ~prev;

            // Release first to avoid stale modifiers piling up.
            if ((toRelease & Direction.Up)    != 0) Release(_upKey);
            if ((toRelease & Direction.Down)  != 0) Release(_downKey);
            if ((toRelease & Direction.Left)  != 0) Release(_leftKey);
            if ((toRelease & Direction.Right) != 0) Release(_rightKey);

            if ((toPress & Direction.Up)    != 0) Press(_upKey);
            if ((toPress & Direction.Down)  != 0) Press(_downKey);
            if ((toPress & Direction.Left)  != 0) Press(_leftKey);
            if ((toPress & Direction.Right) != 0) Press(_rightKey);
        }

        private void Press(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _keyboard.KeyDown(key);
        }

        private void Release(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _keyboard.KeyUp(key);
        }
    }

    /// <summary>
    /// Mutable state shared across functors that target the same 4-key combination.
    /// </summary>
    private sealed class ButtonsState
    {
        public Direction Pressed { get; set; } = Direction.None;
    }
}
