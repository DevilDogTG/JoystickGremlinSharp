// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.EmuWheel;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.EmuWheel;

/// <summary>
/// Descriptor for the EmuWheel button action. Maps a physical button or axis input to a virtual wheel button output.
/// Configuration keys: <c>vjoyId</c> (uint, default 2), <c>buttonIndex</c> (int, default 1),
/// <c>threshold</c> (double 0–1, default 0.5 — axis value above which the virtual button is pressed).
/// The threshold is only applied for analog axis source inputs; direct button mappings remain direct.
/// </summary>
public sealed class EmuWheelButtonDescriptor : IActionDescriptor
{
    private readonly IEmuWheelDeviceManager _deviceManager;
    private readonly ILogger<EmuWheelButtonDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "emuwheel-button";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "EmuWheel Button";

    /// <summary>
    /// Initializes a new instance of <see cref="EmuWheelButtonDescriptor"/>.
    /// </summary>
    public EmuWheelButtonDescriptor(IEmuWheelDeviceManager deviceManager, ILogger<EmuWheelButtonDescriptor> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 2);
        var buttonIndex = configuration?["buttonIndex"]?.GetValue<int>() ?? 1;
        var threshold = configuration?["threshold"]?.GetValue<double>() ?? 0.5;
        threshold = Math.Clamp(threshold, 0.0, 1.0);
        return new EmuWheelButtonFunctor(_deviceManager, vjoyId, buttonIndex, threshold, _logger);
    }

    private sealed class EmuWheelButtonFunctor : IActionFunctor
    {
        private readonly IEmuWheelDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _buttonIndex;
        private readonly double _threshold;
        private readonly ILogger _logger;

        internal EmuWheelButtonFunctor(IEmuWheelDeviceManager manager, uint vjoyId, int buttonIndex, double threshold, ILogger logger)
        {
            _manager = manager;
            _vjoyId = vjoyId;
            _buttonIndex = buttonIndex;
            _threshold = threshold;
            _logger = logger;
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                var device = _manager.AcquireDevice(_vjoyId);
                bool pressed = GetPressedState(inputEvent);
                _logger.LogDebug(
                    "EmuWheel button: source {SourceDeviceGuid} input {Identifier} value {Value} -> device {VJoyId} button {ButtonIndex} pressed {Pressed}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    inputEvent.Value,
                    _vjoyId,
                    _buttonIndex,
                    pressed);
                try
                {
                    device.SetButton(_buttonIndex, pressed);
                }
                catch (EmuWheelException)
                {
                    _logger.LogWarning(
                        "EmuWheel device {VJoyId} ownership lost; re-acquiring and retrying button {ButtonIndex}",
                        _vjoyId,
                        _buttonIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetButton(_buttonIndex, pressed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "EmuWheel button action failed for source {SourceDeviceGuid} input {Identifier} -> device {VJoyId} button {ButtonIndex}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    _vjoyId,
                    _buttonIndex);
            }

            return Task.CompletedTask;
        }

        private bool GetPressedState(InputEvent inputEvent) => inputEvent.InputType switch
        {
            InputType.JoystickAxis => inputEvent.Value >= _threshold,
            InputType.MouseAxis    => inputEvent.Value >= _threshold,
            InputType.JoystickHat  => inputEvent.Value >= 0.0,
            _                      => inputEvent.Value >= 0.5,
        };
    }
}
