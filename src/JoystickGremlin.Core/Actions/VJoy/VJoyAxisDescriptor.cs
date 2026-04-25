// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
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
                var device = _manager.GetDevice(_vjoyId);
                device.SetAxis(_axisIndex, inputEvent.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "vJoy axis action failed for device {Id} axis {Axis}", _vjoyId, _axisIndex);
            }

            return Task.CompletedTask;
        }
    }
}
