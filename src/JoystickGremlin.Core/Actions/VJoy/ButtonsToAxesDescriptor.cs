// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.VJoy;

/// <summary>
/// Descriptor for mapping 4 physical buttons (Up/Down/Left/Right) to dual vJoy axes (Y and X).
/// 
/// The functor maintains shared state across all 4 button inputs and calculates both axis values.
/// Up/Down buttons control Y-axis; Left/Right buttons control X-axis.
/// State is shared across all functors configured for the same vJoy device and axis indices.
/// </summary>
/// <remarks>
/// Configuration keys:
/// <list type="bullet">
///   <item><c>vjoyId</c> (uint, default 1) — target vJoy device ID.</item>
///   <item><c>yAxisIndex</c> (int, default 2) — target Y-axis index (1-based); Up=+1.0, Down=-1.0.</item>
///   <item><c>xAxisIndex</c> (int, default 1) — target X-axis index (1-based); Right=+1.0, Left=-1.0.</item>
///   <item><c>upButtonId</c> (int, required) — physical button index for Up direction (Y axis +1.0).</item>
///   <item><c>downButtonId</c> (int, required) — physical button index for Down direction (Y axis -1.0).</item>
///   <item><c>leftButtonId</c> (int, required) — physical button index for Left direction (X axis -1.0).</item>
///   <item><c>rightButtonId</c> (int, required) — physical button index for Right direction (X axis +1.0).</item>
/// </list>
/// </remarks>
public sealed class ButtonsToAxesDescriptor : IActionDescriptor
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<ButtonsToAxesDescriptor> _logger;

    // Shared state across all functors for the same axes config: key = "vjoyId:xAxisIndex:yAxisIndex"
    private readonly Dictionary<string, ButtonsToAxesState> _sharedStates = new(StringComparer.Ordinal);
    private readonly object _stateLock = new();

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "buttons-to-axes";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "Buttons to Axes";

    /// <summary>
    /// Initializes a new instance of <see cref="ButtonsToAxesDescriptor"/>.
    /// </summary>
    public ButtonsToAxesDescriptor(IVirtualDeviceManager virtualDeviceManager, ILogger<ButtonsToAxesDescriptor> logger)
    {
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 1);
        var xAxisIndex = configuration?["xAxisIndex"]?.GetValue<int>() ?? 1;
        var yAxisIndex = configuration?["yAxisIndex"]?.GetValue<int>() ?? 2;
        var upButtonId = configuration?["upButtonId"]?.GetValue<int>() ?? -1;
        var downButtonId = configuration?["downButtonId"]?.GetValue<int>() ?? -1;
        var leftButtonId = configuration?["leftButtonId"]?.GetValue<int>() ?? -1;
        var rightButtonId = configuration?["rightButtonId"]?.GetValue<int>() ?? -1;

        return new ButtonsToAxesFunctor(
            _virtualDeviceManager,
            vjoyId,
            xAxisIndex,
            yAxisIndex,
            upButtonId,
            downButtonId,
            leftButtonId,
            rightButtonId,
            this,
            _logger);
    }

    private sealed class ButtonsToAxesFunctor : IActionFunctor
    {
        private readonly IVirtualDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _xAxisIndex;
        private readonly int _yAxisIndex;
        private readonly int _upButtonId;
        private readonly int _downButtonId;
        private readonly int _leftButtonId;
        private readonly int _rightButtonId;
        private readonly ButtonsToAxesDescriptor _descriptor;
        private readonly ILogger _logger;
        private readonly string _stateKey;

        internal ButtonsToAxesFunctor(
            IVirtualDeviceManager manager,
            uint vjoyId,
            int xAxisIndex,
            int yAxisIndex,
            int upButtonId,
            int downButtonId,
            int leftButtonId,
            int rightButtonId,
            ButtonsToAxesDescriptor descriptor,
            ILogger logger)
        {
            _manager = manager;
            _vjoyId = vjoyId;
            _xAxisIndex = xAxisIndex;
            _yAxisIndex = yAxisIndex;
            _upButtonId = upButtonId;
            _downButtonId = downButtonId;
            _leftButtonId = leftButtonId;
            _rightButtonId = rightButtonId;
            _descriptor = descriptor;
            _logger = logger;
            _stateKey = $"{vjoyId}:{xAxisIndex}:{yAxisIndex}";
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
                        "ButtonsToAxes: ignoring event for button {Identifier} (not a configured button)",
                        inputEvent.Identifier);
                    return Task.CompletedTask;
                }

                bool isPressed = inputEvent.Value >= 0.5;

                _logger.LogTrace(
                    "ButtonsToAxes: {Direction} button {ButtonId} {State}",
                    buttonInput.Value,
                    inputEvent.Identifier,
                    isPressed ? "pressed" : "released");

                // Update shared state
                DirectionalButtonState newState;
                lock (_descriptor._stateLock)
                {
                    if (!_descriptor._sharedStates.TryGetValue(_stateKey, out var state))
                    {
                        state = new ButtonsToAxesState();
                        _descriptor._sharedStates[_stateKey] = state;
                    }

                    state.CurrentState = ButtonToOutputState.UpdateState(
                        state.CurrentState,
                        buttonInput.Value,
                        isPressed);
                    newState = state.CurrentState;
                }

                // Calculate axis values and send both axes to vJoy atomically
                double yAxisValue = ButtonToOutputState.CalculateAxisValue(newState, isYAxis: true);
                double xAxisValue = ButtonToOutputState.CalculateAxisValue(newState, isYAxis: false);

                var device = _manager.GetOrAcquireDevice(_vjoyId);
                try
                {
                    device.SetAxis(_xAxisIndex, xAxisValue);
                    device.SetAxis(_yAxisIndex, yAxisValue);
                    _logger.LogDebug(
                        "ButtonsToAxes: vJoy {VJoyId} X-axis {XAxisIndex}={XValue}, Y-axis {YAxisIndex}={YValue}",
                        _vjoyId,
                        _xAxisIndex,
                        xAxisValue,
                        _yAxisIndex,
                        yAxisValue);
                }
                catch (VJoyException)
                {
                    _logger.LogWarning(
                        "vJoy device {VJoyId} ownership lost; re-acquiring and retrying axes {XAxisIndex},{YAxisIndex}",
                        _vjoyId,
                        _xAxisIndex,
                        _yAxisIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetAxis(_xAxisIndex, xAxisValue);
                    device.SetAxis(_yAxisIndex, yAxisValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ButtonsToAxes action failed for vJoy device {VJoyId} axes {XAxisIndex},{YAxisIndex}",
                    _vjoyId,
                    _xAxisIndex,
                    _yAxisIndex);
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
    /// Holds the runtime state for a buttons-to-axes mapping.
    /// </summary>
    private sealed class ButtonsToAxesState
    {
        /// <summary>Current state of the four directional buttons.</summary>
        public DirectionalButtonState CurrentState { get; set; } = DirectionalButtonState.None;
    }
}
