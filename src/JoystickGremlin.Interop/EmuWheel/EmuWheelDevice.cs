// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.EmuWheel;

namespace JoystickGremlin.Interop.EmuWheel;

/// <summary>
/// Wraps an <see cref="IVirtualDevice"/> (vJoy) and adds wheel model identity for the EmuWheel backend.
/// All axis/button/hat I/O is delegated to the inner virtual device.
/// </summary>
internal sealed class EmuWheelDevice : IEmuWheelDevice
{
    private readonly IVirtualDevice _inner;

    /// <summary>
    /// Initializes a new instance of <see cref="EmuWheelDevice"/>.
    /// </summary>
    /// <param name="inner">The underlying vJoy virtual device.</param>
    /// <param name="wheelModel">The wheel model this device is presenting as.</param>
    internal EmuWheelDevice(IVirtualDevice inner, WheelModel wheelModel)
    {
        _inner = inner;
        WheelModel = wheelModel;
    }

    /// <inheritdoc/>
    public uint DeviceId => _inner.DeviceId;

    /// <inheritdoc/>
    public WheelModel WheelModel { get; }

    /// <inheritdoc/>
    public int AxisCount => _inner.AxisCount;

    /// <inheritdoc/>
    public int ButtonCount => _inner.ButtonCount;

    /// <inheritdoc/>
    public int HatCount => _inner.HatCount;

    /// <inheritdoc/>
    public void SetAxis(int axisIndex, double value) => _inner.SetAxis(axisIndex, value);

    /// <inheritdoc/>
    public void SetButton(int buttonIndex, bool pressed) => _inner.SetButton(buttonIndex, pressed);

    /// <inheritdoc/>
    public void SetHat(int hatIndex, int degrees) => _inner.SetHat(hatIndex, degrees);

    /// <inheritdoc/>
    public double? GetAxis(int axisIndex) => _inner.GetAxis(axisIndex);

    /// <inheritdoc/>
    public bool? GetButton(int buttonIndex) => _inner.GetButton(buttonIndex);

    /// <inheritdoc/>
    public int? GetHat(int hatIndex) => _inner.GetHat(hatIndex);

    /// <inheritdoc/>
    public void Reset() => _inner.Reset();
}
