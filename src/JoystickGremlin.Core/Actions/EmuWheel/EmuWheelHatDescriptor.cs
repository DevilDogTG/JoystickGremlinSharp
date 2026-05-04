// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.EmuWheel;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.EmuWheel;

/// <summary>
/// Descriptor for the EmuWheel hat action. Maps a physical hat input to a virtual wheel POV/hat output.
/// Configuration keys: <c>vjoyId</c> (uint, default 2), <c>hatIndex</c> (int, default 1).
/// </summary>
public sealed class EmuWheelHatDescriptor : IActionDescriptor
{
    private readonly IEmuWheelDeviceManager _deviceManager;
    private readonly ILogger<EmuWheelHatDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "emuwheel-hat";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "EmuWheel Hat";

    /// <summary>
    /// Initializes a new instance of <see cref="EmuWheelHatDescriptor"/>.
    /// </summary>
    public EmuWheelHatDescriptor(IEmuWheelDeviceManager deviceManager, ILogger<EmuWheelHatDescriptor> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId = (uint)(configuration?["vjoyId"]?.GetValue<int>() ?? 2);
        var hatIndex = configuration?["hatIndex"]?.GetValue<int>() ?? 1;
        return new EmuWheelHatFunctor(_deviceManager, vjoyId, hatIndex, _logger);
    }

    private sealed class EmuWheelHatFunctor : IActionFunctor
    {
        private readonly IEmuWheelDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _hatIndex;
        private readonly ILogger _logger;

        internal EmuWheelHatFunctor(IEmuWheelDeviceManager manager, uint vjoyId, int hatIndex, ILogger logger)
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
                    "EmuWheel hat: source {SourceDeviceGuid} input {Identifier} value {Value} -> device {VJoyId} hat {HatIndex}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    inputEvent.Value,
                    _vjoyId,
                    _hatIndex);
                int degrees = inputEvent.Value < 0.0 ? -1 : (int)(inputEvent.Value * 35999.0);
                var device = _manager.AcquireDevice(_vjoyId);
                try
                {
                    device.SetHat(_hatIndex, degrees);
                }
                catch (EmuWheelException)
                {
                    _logger.LogWarning(
                        "EmuWheel device {VJoyId} ownership lost; re-acquiring and retrying hat {HatIndex}",
                        _vjoyId,
                        _hatIndex);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    device.SetHat(_hatIndex, degrees);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "EmuWheel hat action failed for source {SourceDeviceGuid} input {Identifier} -> device {VJoyId} hat {HatIndex}",
                    inputEvent.DeviceGuid,
                    inputEvent.Identifier,
                    _vjoyId,
                    _hatIndex);
            }

            return Task.CompletedTask;
        }
    }
}
