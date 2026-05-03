// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.VJoy;

/// <summary>
/// Descriptor for mapping 4 physical buttons (Up/Down/Left/Right) to a virtual D-Pad (Hat/POV).
/// 
/// The functor maintains state across all 4 button inputs and calculates the resulting hat direction.
/// State is shared across all functors configured for the same vJoy device and hat index.
/// </summary>
/// <remarks>
/// Configuration keys:
/// <list type="bullet">
///   <item><c>vjoyId</c> (uint, default 1) — target vJoy device ID.</item>
///   <item><c>hatIndex</c> (int, default 1) — target hat/POV index (1-based).</item>
///   <item><c>upButtonId</c> (int, required) — physical button index for Up direction.</item>
///   <item><c>downButtonId</c> (int, required) — physical button index for Down direction.</item>
///   <item><c>leftButtonId</c> (int, required) — physical button index for Left direction.</item>
///   <item><c>rightButtonId</c> (int, required) — physical button index for Right direction.</item>
/// </list>
/// </remarks>
public sealed class ButtonsToHatDescriptor : IActionDescriptor
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<ButtonsToHatDescriptor> _logger;

    // Shared state across all functors for the same hat config: key = "vjoyId:hatIndex"
    private readonly Dictionary<string, ButtonsToHatState> _sharedStates = new(StringComparer.Ordinal);
    private readonly object _stateLock = new();

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "buttons-to-hat";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "Buttons to Hat";

    /// <summary>
    /// Initializes a new instance of <see cref="ButtonsToHatDescriptor"/>.
    /// </summary>
    public ButtonsToHatDescriptor(IVirtualDeviceManager virtualDeviceManager, ILogger<ButtonsToHatDescriptor> logger)
    {
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 1);
        var hatIndex = configuration?["hatIndex"]?.GetValue<int>() ?? 1;
        var upButtonId = configuration?["upButtonId"]?.GetValue<int>() ?? -1;
        var downButtonId = configuration?["downButtonId"]?.GetValue<int>() ?? -1;
        var leftButtonId = configuration?["leftButtonId"]?.GetValue<int>() ?? -1;
        var rightButtonId = configuration?["rightButtonId"]?.GetValue<int>() ?? -1;

        return new ButtonsToHatFunctor(
            _virtualDeviceManager,
            vjoyId,
            hatIndex,
            upButtonId,
            downButtonId,
            leftButtonId,
            rightButtonId,
            this,
            _logger);
    }

    private sealed class ButtonsToHatFunctor : IActionFunctor
    {
        private readonly IVirtualDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _hatIndex;
        private readonly int _upButtonId;
        private readonly int _downButtonId;
        private readonly int _leftButtonId;
        private readonly int _rightButtonId;
        private readonly ButtonsToHatDescriptor _descriptor;
        private readonly ILogger _logger;
        private readonly string _stateKey;

        internal ButtonsToHatFunctor(
            IVirtualDeviceManager manager,
            uint vjoyId,
            int hatIndex,
            int upButtonId,
            int downButtonId,
            int leftButtonId,
            int rightButtonId,
            ButtonsToHatDescriptor descriptor,
            ILogger logger)
        {
            _manager = manager;
            _vjoyId = vjoyId;
            _hatIndex = hatIndex;
            _upButtonId = upButtonId;
            _downButtonId = downButtonId;
            _leftButtonId = leftButtonId;
            _rightButtonId = rightButtonId;
            _descriptor = descriptor;
            _logger = logger;
            _stateKey = $"{vjoyId}:{hatIndex}";
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                // Determine which button this event is for
                var buttonInput = DetermineButtonInput(inputEvent.Identifier);
                if (buttonInput is null)
                {
                    _logger.LogTrace(
                        "ButtonsToHat: ignoring event for button {Identifier} (not a configured button)",
                        inputEvent.Identifier);
                    return Task.CompletedTask;
                }

                bool isPressed = inputEvent.Value >= 0.5;

                _logger.LogTrace(
                    "ButtonsToHat: {Direction} button {ButtonId} {State}",
                    buttonInput.Value,
                    inputEvent.Identifier,
                    isPressed ? "pressed" : "released");

                // Update shared state
                DirectionalButtonState newState;
                lock (_descriptor._stateLock)
                {
                    if (!_descriptor._sharedStates.TryGetValue(_stateKey, out var state))
                    {
                        state = new ButtonsToHatState();
                        _descriptor._sharedStates[_stateKey] = state;
                    }

                    state.CurrentState = ButtonToOutputState.UpdateState(
                        state.CurrentState,
                        buttonInput.Value,
                        isPressed);
                    newState = state.CurrentState;
                }

                // Calculate hat degrees and send to vJoy
                int hatDegrees = ButtonToOutputState.CalculateHatDegrees(newState);
                var device = _manager.GetOrAcquireDevice(_vjoyId);
                try
                {
                    device.SetHat(_hatIndex, hatDegrees);
                    _logger.LogDebug(
                        "ButtonsToHat: hat {HatIndex} on vJoy {VJoyId} set to {Degrees}°",
                        _hatIndex,
                        _vjoyId,
                        hatDegrees);
                }
                catch (VJoyException)
                {
                    _logger.LogWarning(
                        "vJoy device {VJoyId} ownership lost; re-acquiring and retrying hat {HatIndex}",
                        _vjoyId,
                        _hatIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetHat(_hatIndex, hatDegrees);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ButtonsToHat action failed for vJoy device {VJoyId} hat {HatIndex}",
                    _vjoyId,
                    _hatIndex);
            }

            return Task.CompletedTask;
        }

        private DirectionalInput? DetermineButtonInput(int buttonIdentifier)
        {
            if (buttonIdentifier == _upButtonId) return DirectionalInput.Up;
            if (buttonIdentifier == _downButtonId) return DirectionalInput.Down;
            if (buttonIdentifier == _leftButtonId) return DirectionalInput.Left;
            if (buttonIdentifier == _rightButtonId) return DirectionalInput.Right;
            return null;
        }
    }

    /// <summary>
    /// Holds the runtime state for a buttons-to-hat mapping.
    /// </summary>
    private sealed class ButtonsToHatState
    {
        /// <summary>Current state of the four directional buttons.</summary>
        public DirectionalButtonState CurrentState { get; set; } = DirectionalButtonState.None;
    }
}
