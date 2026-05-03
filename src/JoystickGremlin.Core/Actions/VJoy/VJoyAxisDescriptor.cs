// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.VJoy;

/// <summary>
/// Descriptor for the vJoy axis action. Maps a physical axis input to a virtual axis output.
/// Configuration keys: <c>vjoyId</c> (uint, default 1), <c>axisIndex</c> (int, default 1).
/// </summary>
public sealed class VJoyAxisDescriptor : IActionDescriptor
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<VJoyAxisDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "vjoy-axis";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "vJoy Axis";

    /// <summary>
    /// Initializes a new instance of <see cref="VJoyAxisDescriptor"/>.
    /// </summary>
    public VJoyAxisDescriptor(IVirtualDeviceManager virtualDeviceManager, ILogger<VJoyAxisDescriptor> logger)
    {
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 1);
        var axisIndex = configuration?["axisIndex"]?.GetValue<int>() ?? 1;
        return new VJoyAxisFunctor(_virtualDeviceManager, vjoyId, axisIndex, _logger);
    }

    private sealed class VJoyAxisFunctor : IActionFunctor
    {
        private readonly IVirtualDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _axisIndex;
        private readonly ILogger _logger;

        internal VJoyAxisFunctor(IVirtualDeviceManager manager, uint vjoyId, int axisIndex, ILogger logger)
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
                    "Executing vJoy axis action: source device {SourceDeviceGuid} input {Identifier} value {Value} -> vJoy {VJoyId} axis {AxisIndex}",
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
                catch (VJoyException)
                {
                    _logger.LogWarning(
                        "vJoy device {VJoyId} ownership lost; re-acquiring and retrying axis {AxisIndex}",
                        _vjoyId,
                        _axisIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetAxis(_axisIndex, inputEvent.Value);
                }
                _logger.LogDebug("vJoy axis action succeeded for device {VJoyId} axis {AxisIndex}", _vjoyId, _axisIndex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "vJoy axis action failed for source device {SourceDeviceGuid} input {Identifier} -> device {Id} axis {Axis}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    _vjoyId,
                    _axisIndex);
            }

            return Task.CompletedTask;
        }
    }
}
