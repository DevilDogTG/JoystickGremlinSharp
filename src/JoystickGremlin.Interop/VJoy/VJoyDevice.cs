// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Exceptions;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Managed representation of a vJoy virtual joystick device.
/// Wraps raw vJoy P/Invoke calls and provides normalised axis input in the range [-1.0, 1.0].
/// </summary>
public sealed class VJoyDevice : IVirtualDevice
{
    /// <summary>
    /// Maps 1-based linear axis index to the <see cref="AxisCode"/> HID usage value.
    /// Index 0 is unused (vJoy uses 1-based axis numbering).
    /// </summary>
    private static readonly uint[] LinearToAxisCode =
    [
        0,                       // [0] unused
        (uint)AxisCode.X,        // [1]
        (uint)AxisCode.Y,        // [2]
        (uint)AxisCode.Z,        // [3]
        (uint)AxisCode.RX,       // [4]
        (uint)AxisCode.RY,       // [5]
        (uint)AxisCode.RZ,       // [6]
        (uint)AxisCode.SL0,      // [7]
        (uint)AxisCode.SL1,      // [8]
    ];

    private readonly uint _vjoyId;

    // Keyed by AxisCode value; stores half the full axis range for normalisation.
    private readonly Dictionary<uint, double> _axisHalfRanges;
    private readonly ConcurrentDictionary<int, double> _axisValues;
    private readonly ConcurrentDictionary<int, bool> _buttonStates;
    private readonly ConcurrentDictionary<int, int> _hatStates;

    private readonly bool _useContHat;

    /// <inheritdoc/>
    public uint DeviceId { get; }

    /// <summary>Gets the number of axes present on this device.</summary>
    public int AxisCount { get; }

    /// <summary>Gets the number of buttons present on this device.</summary>
    public int ButtonCount { get; }

    /// <summary>Gets the number of hat switches present on this device.</summary>
    public int HatCount { get; }

    internal VJoyDevice(uint vjoyId)
    {
        _vjoyId = vjoyId;
        DeviceId = vjoyId;

        _axisHalfRanges = [];
        _axisValues = new();
        _buttonStates = new();
        _hatStates = new();
        int axisCount = 0;
        foreach (AxisCode code in Enum.GetValues<AxisCode>())
        {
            if (VJoyNative.GetVJDAxisExist(vjoyId, (uint)code) > 0)
            {
                VJoyNative.GetVJDAxisMax(vjoyId, (uint)code, out uint max);
                _axisHalfRanges[(uint)code] = max / 2.0;
                _axisValues[axisCount + 1] = 0.0;
                axisCount++;
            }
        }

        AxisCount = axisCount;
        ButtonCount = VJoyNative.GetVJDButtonNumber(vjoyId);
        for (var buttonIndex = 1; buttonIndex <= ButtonCount; buttonIndex++)
            _buttonStates[buttonIndex] = false;

        int contHats = VJoyNative.GetVJDContPovNumber(vjoyId);
        int discHats = VJoyNative.GetVJDDiscPovNumber(vjoyId);
        HatCount = Math.Max(contHats, discHats);
        _useContHat = contHats > 0;
        for (var hatIndex = 1; hatIndex <= HatCount; hatIndex++)
            _hatStates[hatIndex] = -1;
    }

    /// <inheritdoc/>
    /// <param name="axisIndex">1-based linear axis index (1=X … 8=SL1).</param>
    /// <param name="value">Normalised value in the range [-1.0, 1.0].</param>
    /// <exception cref="VJoyException">Thrown if the axis could not be set.</exception>
    public void SetAxis(int axisIndex, double value)
    {
        if (axisIndex < 1 || axisIndex > 8)
            throw new VJoyException($"Axis index {axisIndex} is out of range (1–8) for device {_vjoyId}");

        uint axisCode = LinearToAxisCode[axisIndex];
        if (!_axisHalfRanges.TryGetValue(axisCode, out double halfRange))
            return; // axis not present on this device — silently ignore

        double clamped = Math.Clamp(value, -1.0, 1.0);
        int rawValue = (int)(halfRange + halfRange * clamped + 0.5);

        if (!VJoyNative.SetAxis(rawValue, _vjoyId, axisCode))
            throw new VJoyException($"Failed to set axis {axisIndex} on vJoy device {_vjoyId}");

        _axisValues[axisIndex] = clamped;
    }

    /// <inheritdoc/>
    /// <param name="buttonIndex">1-based button index.</param>
    /// <param name="pressed"><c>true</c> = pressed, <c>false</c> = released.</param>
    /// <exception cref="VJoyException">Thrown if the button state could not be set.</exception>
    public void SetButton(int buttonIndex, bool pressed)
    {
        if (!VJoyNative.SetBtn(pressed, _vjoyId, (byte)buttonIndex))
            throw new VJoyException($"Failed to set button {buttonIndex} on vJoy device {_vjoyId}");

        _buttonStates[buttonIndex] = pressed;
    }

    /// <inheritdoc/>
    /// <param name="hatIndex">1-based hat index.</param>
    /// <param name="degrees">Direction in hundredths of a degree (0–35999), or -1 for centre.</param>
    /// <exception cref="VJoyException">Thrown if the hat value could not be set.</exception>
    public void SetHat(int hatIndex, int degrees)
    {
        if (_useContHat)
        {
            // Centre is represented as 0xFFFFFFFF for continuous hats.
            uint contValue = degrees < 0 ? uint.MaxValue : (uint)degrees;
            if (!VJoyNative.SetContPov(contValue, _vjoyId, (byte)hatIndex))
                throw new VJoyException($"Failed to set continuous hat {hatIndex} on vJoy device {_vjoyId}");
        }
        else
        {
            if (!VJoyNative.SetDiscPov(degrees, _vjoyId, (byte)hatIndex))
                throw new VJoyException($"Failed to set discrete hat {hatIndex} on vJoy device {_vjoyId}");
        }

        _hatStates[hatIndex] = degrees;
    }

    /// <inheritdoc/>
    public double? GetAxis(int axisIndex) =>
        _axisValues.TryGetValue(axisIndex, out var value) ? value : null;

    /// <inheritdoc/>
    public bool? GetButton(int buttonIndex) =>
        _buttonStates.TryGetValue(buttonIndex, out var value) ? value : null;

    /// <inheritdoc/>
    public int? GetHat(int hatIndex) =>
        _hatStates.TryGetValue(hatIndex, out var value) ? value : null;

    /// <inheritdoc/>
    public void Reset()
    {
        VJoyNative.ResetVJD(_vjoyId);

        foreach (var axisIndex in _axisValues.Keys.ToList())
            _axisValues[axisIndex] = 0.0;

        foreach (var buttonIndex in _buttonStates.Keys.ToList())
            _buttonStates[buttonIndex] = false;

        foreach (var hatIndex in _hatStates.Keys.ToList())
            _hatStates[hatIndex] = -1;
    }
}
