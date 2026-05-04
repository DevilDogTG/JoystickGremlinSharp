// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.VJoy;

/// <summary>
/// Descriptor for the vJoy button action. Maps a physical button or axis input to a virtual button output.
/// Configuration keys: <c>vjoyId</c> (uint, default 1), <c>buttonIndex</c> (int, default 1),
/// <c>threshold</c> (double 0–1, default 0.5 — axis value above which the virtual button is pressed).
/// Lower values create a hair-trigger (e.g. 0.05); higher values require deeper travel (e.g. 0.9).
/// The threshold is only applied for analog axis source inputs; direct button mappings remain direct.
/// </summary>
public sealed class VJoyButtonDescriptor : IActionDescriptor
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<VJoyButtonDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "vjoy-button";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "vJoy Button";

    /// <summary>
    /// Initializes a new instance of <see cref="VJoyButtonDescriptor"/>.
    /// </summary>
    public VJoyButtonDescriptor(IVirtualDeviceManager virtualDeviceManager, ILogger<VJoyButtonDescriptor> logger)
    {
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 1);
        var buttonIndex = configuration?["buttonIndex"]?.GetValue<int>() ?? 1;
        var threshold = configuration?["threshold"]?.GetValue<double>() ?? 0.5;
        threshold = Math.Clamp(threshold, 0.0, 1.0);
        return new VJoyButtonFunctor(_virtualDeviceManager, vjoyId, buttonIndex, threshold, _logger);
    }

    private sealed class VJoyButtonFunctor : IActionFunctor
    {
        private readonly IVirtualDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _buttonIndex;
        private readonly double _threshold;
        private readonly ILogger _logger;

        internal VJoyButtonFunctor(IVirtualDeviceManager manager, uint vjoyId, int buttonIndex, double threshold, ILogger logger)
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
                _logger.LogDebug(
                    "Executing vJoy button action: source device {SourceDeviceGuid} input {Identifier} value {Value} -> vJoy {VJoyId} button {ButtonIndex}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    inputEvent.Value,
                    _vjoyId,
                    _buttonIndex);
                var device = _manager.GetOrAcquireDevice(_vjoyId);
                bool pressed = GetPressedState(inputEvent);
                try
                {
                    device.SetButton(_buttonIndex, pressed);
                }
                catch (VJoyException)
                {
                    // Device ownership may have been lost (another app took control).
                    // Release, re-acquire, and retry once.
                    _logger.LogWarning(
                        "vJoy device {VJoyId} ownership lost; re-acquiring and retrying button {ButtonIndex}",
                        _vjoyId,
                        _buttonIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetButton(_buttonIndex, pressed);
                }
                _logger.LogDebug(
                    "vJoy button action succeeded for device {VJoyId} button {ButtonIndex} pressed {Pressed}",
                    _vjoyId,
                    _buttonIndex,
                    pressed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "vJoy button action failed for source device {SourceDeviceGuid} input {Identifier} -> device {Id} button {Button}",
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
            InputType.MouseAxis => inputEvent.Value >= _threshold,
            InputType.JoystickHat => inputEvent.Value >= 0.0,
            _ => inputEvent.Value >= 0.5,
        };
    }
}
