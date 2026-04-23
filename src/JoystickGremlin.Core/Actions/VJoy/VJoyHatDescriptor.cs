// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.VJoy;

/// <summary>
/// Descriptor for the vJoy hat action. Maps a physical hat input to a virtual hat/POV output.
/// Configuration keys: <c>vjoyId</c> (uint, default 1), <c>hatIndex</c> (int, default 1).
/// Input value convention: degrees 0–35999, or -1 for center.
/// </summary>
public sealed class VJoyHatDescriptor : IActionDescriptor
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<VJoyHatDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "vjoy-hat";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "vJoy Hat";

    /// <summary>
    /// Initializes a new instance of <see cref="VJoyHatDescriptor"/>.
    /// </summary>
    public VJoyHatDescriptor(IVirtualDeviceManager virtualDeviceManager, ILogger<VJoyHatDescriptor> logger)
    {
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 1);
        var hatIndex = configuration?["hatIndex"]?.GetValue<int>() ?? 1;
        return new VJoyHatFunctor(_virtualDeviceManager, vjoyId, hatIndex, _logger);
    }

    private sealed class VJoyHatFunctor : IActionFunctor
    {
        private readonly IVirtualDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _hatIndex;
        private readonly ILogger _logger;

        internal VJoyHatFunctor(IVirtualDeviceManager manager, uint vjoyId, int hatIndex, ILogger logger)
        {
            _manager = manager;
            _vjoyId = vjoyId;
            _hatIndex = hatIndex;
            _logger = logger;
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                var device = _manager.GetDevice(_vjoyId);
                // Value is degrees (0–35999) for directional, or -1 for center.
                device.SetHat(_hatIndex, (int)inputEvent.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "vJoy hat action failed for device {Id} hat {Hat}", _vjoyId, _hatIndex);
            }

            return Task.CompletedTask;
        }
    }
}
