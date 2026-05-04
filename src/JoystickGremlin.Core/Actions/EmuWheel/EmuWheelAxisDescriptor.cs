// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.EmuWheel;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.EmuWheel;

/// <summary>
/// Descriptor for the EmuWheel axis action. Maps a physical axis input to a virtual wheel axis output.
/// Configuration keys: <c>vjoyId</c> (uint, default 2), <c>axisIndex</c> (int, default 1).
/// </summary>
public sealed class EmuWheelAxisDescriptor : IActionDescriptor
{
    private readonly IEmuWheelDeviceManager _deviceManager;
    private readonly ILogger<EmuWheelAxisDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "emuwheel-axis";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "EmuWheel Axis";

    /// <summary>
    /// Initializes a new instance of <see cref="EmuWheelAxisDescriptor"/>.
    /// </summary>
    public EmuWheelAxisDescriptor(IEmuWheelDeviceManager deviceManager, ILogger<EmuWheelAxisDescriptor> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 2);
        var axisIndex = configuration?["axisIndex"]?.GetValue<int>() ?? 1;
        return new EmuWheelAxisFunctor(_deviceManager, vjoyId, axisIndex, _logger);
    }

    private sealed class EmuWheelAxisFunctor : IActionFunctor
    {
        private readonly IEmuWheelDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _axisIndex;
        private readonly ILogger _logger;

        internal EmuWheelAxisFunctor(IEmuWheelDeviceManager manager, uint vjoyId, int axisIndex, ILogger logger)
        {
            _manager = manager;
            _vjoyId = vjoyId;
            _axisIndex = axisIndex;
            _logger = logger;
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug(
                    "EmuWheel axis: source {SourceDeviceGuid} input {Identifier} value {Value} -> device {VJoyId} axis {AxisIndex}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    inputEvent.Value,
                    _vjoyId,
                    _axisIndex);
                var device = _manager.GetOrAcquireDevice(_vjoyId);
                try
                {
                    device.SetAxis(_axisIndex, inputEvent.Value);
                }
                catch (EmuWheelException)
                {
                    _logger.LogWarning(
                        "EmuWheel device {VJoyId} ownership lost; re-acquiring and retrying axis {AxisIndex}",
                        _vjoyId,
                        _axisIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetAxis(_axisIndex, inputEvent.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "EmuWheel axis action failed for source {SourceDeviceGuid} input {Identifier} -> device {VJoyId} axis {AxisIndex}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    _vjoyId,
                    _axisIndex);
            }

            return Task.CompletedTask;
        }
    }
}
