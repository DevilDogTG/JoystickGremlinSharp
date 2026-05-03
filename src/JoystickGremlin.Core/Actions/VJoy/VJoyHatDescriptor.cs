// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
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
                _logger.LogDebug(
                    "Executing vJoy hat action: source device {SourceDeviceGuid} input {Identifier} value {Value} -> vJoy {VJoyId} hat {HatIndex}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    inputEvent.Value,
                    _vjoyId,
                    _hatIndex);
                var device = _manager.GetOrAcquireDevice(_vjoyId);
                int degrees = (int)inputEvent.Value;
                try
                {
                    device.SetHat(_hatIndex, degrees);
                }
                catch (VJoyException)
                {
                    _logger.LogWarning(
                        "vJoy device {VJoyId} ownership lost; re-acquiring and retrying hat {HatIndex}",
                        _vjoyId,
                        _hatIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetHat(_hatIndex, degrees);
                }
                _logger.LogDebug("vJoy hat action succeeded for device {VJoyId} hat {HatIndex}", _vjoyId, _hatIndex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "vJoy hat action failed for source device {SourceDeviceGuid} input {Identifier} -> device {Id} hat {Hat}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    _vjoyId,
                    _hatIndex);
            }

            return Task.CompletedTask;
        }
    }
}
