// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Actions.VJoy;

/// <summary>
/// Descriptor for the hat-to-axis action. Maps a physical hat/POV input to one or two virtual axes.
/// </summary>
/// <remarks>
/// Configuration keys:
/// <list type="bullet">
///   <item><c>vjoyId</c> (uint, default 1) — target vJoy device ID.</item>
///   <item><c>xAxisIndex</c> (int, default 1) — target X-axis index (1-based); 0 = disabled.</item>
///   <item><c>yAxisIndex</c> (int, default 2) — target Y-axis index (1-based); 0 = disabled.</item>
/// </list>
/// Hat value convention: degrees 0–35999 (centidegrees), or -1 for center.
/// The functor normalises values from both conventions:
/// whole-degree values (0–359) and centidegree values (0–35999).
/// Direction mapping: 0°=North(Y+), 90°=East(X+), 180°=South(Y-), 270°=West(X-).
/// Diagonal directions resolve to sin/cos components (~0.707 each).
/// </remarks>
public sealed class HatToAxisDescriptor : IActionDescriptor
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<HatToAxisDescriptor> _logger;

    /// <summary>Action tag registered in <see cref="IActionRegistry"/>.</summary>
    public const string ActionTag = "hat-to-axis";

    /// <inheritdoc/>
    public string Tag => ActionTag;

    /// <inheritdoc/>
    public string Name => "Hat to Axes";

    /// <summary>
    /// Initializes a new instance of <see cref="HatToAxisDescriptor"/>.
    /// </summary>
    public HatToAxisDescriptor(IVirtualDeviceManager virtualDeviceManager, ILogger<HatToAxisDescriptor> logger)
    {
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IActionFunctor CreateFunctor(JsonObject? configuration)
    {
        var vjoyId     = (uint)(configuration?["vjoyId"]?.GetValue<int>()     ?? 1);
        var xAxisIndex = configuration?["xAxisIndex"]?.GetValue<int>() ?? 1;
        var yAxisIndex = configuration?["yAxisIndex"]?.GetValue<int>() ?? 2;
        return new HatToAxisFunctor(_virtualDeviceManager, vjoyId, xAxisIndex, yAxisIndex, _logger);
    }

    private sealed class HatToAxisFunctor : IActionFunctor
    {
        private readonly IVirtualDeviceManager _manager;
        private readonly uint _vjoyId;
        private readonly int _xAxisIndex;
        private readonly int _yAxisIndex;
        private readonly ILogger _logger;

        internal HatToAxisFunctor(
            IVirtualDeviceManager manager,
            uint vjoyId,
            int xAxisIndex,
            int yAxisIndex,
            ILogger logger)
        {
            _manager    = manager;
            _vjoyId     = vjoyId;
            _xAxisIndex = xAxisIndex;
            _yAxisIndex = yAxisIndex;
            _logger     = logger;
        }

        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            try
            {
                var (xValue, yValue) = ComputeAxisValues(inputEvent.Value);

                var device = _manager.GetOrAcquireDevice(_vjoyId);
                try
                {
                    WriteAxes(device, xValue, yValue);
                }
                catch (VJoyException)
                {
                    _logger.LogWarning(
                        "vJoy device {VJoyId} ownership lost; re-acquiring and retrying hat-to-axis",
                        _vjoyId);
                    device = _manager.ForceReacquireDevice(_vjoyId);
                    WriteAxes(device, xValue, yValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "HatToAxis failed for vJoy {VJoyId} X-axis {XAxisIndex}, Y-axis {YAxisIndex}",
                    _vjoyId,
                    _xAxisIndex,
                    _yAxisIndex);
            }

            return Task.CompletedTask;
        }

        private void WriteAxes(IVirtualDevice device, double xValue, double yValue)
        {
            if (_xAxisIndex > 0)
                device.SetAxis(_xAxisIndex, xValue);
            if (_yAxisIndex > 0)
                device.SetAxis(_yAxisIndex, yValue);
        }

        /// <summary>
        /// Converts a hat value to normalised X/Y axis values in [-1.0, 1.0].
        /// </summary>
        /// <param name="hatValue">Hat value: -1 = center; 0–359 degrees; 0–35999 centidegrees.</param>
        /// <returns>Tuple of (x, y) axis values.</returns>
        internal static (double x, double y) ComputeAxisValues(double hatValue)
        {
            if (hatValue < 0)
                return (0.0, 0.0);

            // Normalise centidegrees to degrees (vJoy POV uses centidegrees; DILL uses degrees).
            double degrees = hatValue >= 360.0 ? hatValue / 100.0 : hatValue;

            double radians = degrees * (Math.PI / 180.0);

            // Joystick convention: 0° = North = Y+, clockwise rotation.
            // X = sin(θ), Y = cos(θ)
            double x = Math.Round(Math.Sin(radians), 4, MidpointRounding.AwayFromZero);
            double y = Math.Round(Math.Cos(radians), 4, MidpointRounding.AwayFromZero);

            return (Math.Clamp(x, -1.0, 1.0), Math.Clamp(y, -1.0, 1.0));
        }
    }
}
