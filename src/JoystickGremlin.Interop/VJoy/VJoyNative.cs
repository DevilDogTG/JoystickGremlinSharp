// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// DirectInput HID usage codes for vJoy axes, matching the vJoy SDK axis identifiers.
/// </summary>
public enum AxisCode : uint
{
    /// <summary>X axis (HID usage 0x30).</summary>
    X = 0x30,
    /// <summary>Y axis (HID usage 0x31).</summary>
    Y = 0x31,
    /// <summary>Z axis (HID usage 0x32).</summary>
    Z = 0x32,
    /// <summary>Rx (rotation X) axis (HID usage 0x33).</summary>
    RX = 0x33,
    /// <summary>Ry (rotation Y) axis (HID usage 0x34).</summary>
    RY = 0x34,
    /// <summary>Rz (rotation Z) axis (HID usage 0x35).</summary>
    RZ = 0x35,
    /// <summary>Slider 0 axis (HID usage 0x36).</summary>
    SL0 = 0x36,
    /// <summary>Slider 1 axis (HID usage 0x37).</summary>
    SL1 = 0x37,
}

/// <summary>
/// Status of a vJoy virtual joystick device.
/// </summary>
public enum VjdStatus
{
    /// <summary>The device is owned by the current process.</summary>
    Owned = 0,
    /// <summary>The device is free (no owner).</summary>
    Free = 1,
    /// <summary>The device is owned by another process.</summary>
    Busy = 2,
    /// <summary>The device is not present / not configured.</summary>
    Missing = 3,
    /// <summary>Unknown / unexpected state.</summary>
    Unknown = 4,
}

/// <summary>
/// Type of hat switch as configured in the vJoy driver.
/// </summary>
public enum HatType
{
    /// <summary>4-way discrete hat (N, NE, S, W, center = -1).</summary>
    Discrete = 0,
    /// <summary>Continuous hat reporting degrees × 100 (e.g. 4500 = NE; -1 = center).</summary>
    Continuous = 1,
}

/// <summary>
/// Raw P/Invoke declarations for the vJoyInterface native DLL.
/// All functions use cdecl calling convention (as used by the Python ctypes.cdll wrapper).
/// </summary>
internal static partial class VJoyNative
{
    private const string DllName = "vJoyInterface.dll";

    // ── General vJoy information ─────────────────────────────────────────────

    /// <summary>Returns the vJoy driver version as a short integer (e.g. 0x218 = v2.1.8).</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern short GetvJoyVersion();

    /// <summary>Returns <c>true</c> if the vJoy driver is installed and running.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool vJoyEnabled();

    /// <summary>Returns the vJoy product string.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
    internal static extern string? GetvJoyProductString();

    /// <summary>Returns the vJoy manufacturer string.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
    internal static extern string? GetvJoyManufacturerString();

    /// <summary>Returns the vJoy serial number string.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.LPWStr)]
    internal static extern string? GetvJoySerialNumberString();

    // ── Device properties ────────────────────────────────────────────────────

    /// <summary>Returns the number of buttons configured on the specified vJoy device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetVJDButtonNumber(uint rID);

    /// <summary>Returns the number of discrete POV (hat) switches configured on the device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetVJDDiscPovNumber(uint rID);

    /// <summary>Returns the number of continuous POV (hat) switches configured on the device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetVJDContPovNumber(uint rID);

    /// <summary>
    /// Returns non-zero if the specified axis exists on the device.
    /// NOTE: Despite the name, the vJoy API returns an int, not a bool.
    /// See http://vjoystick.sourceforge.net/site/index.php/forum/5-Discussion/1026
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetVJDAxisExist(uint rID, uint axis);

    /// <summary>Reads the maximum value of the specified axis into <paramref name="max"/>.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVJDAxisMax(uint rID, uint axis, out uint max);

    /// <summary>Reads the minimum value of the specified axis into <paramref name="min"/>.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVJDAxisMin(uint rID, uint axis, out uint min);

    // ── Device management ────────────────────────────────────────────────────

    /// <summary>Returns the PID of the process that currently owns the specified vJoy device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetOwnerPid(uint rID);

    /// <summary>Acquires ownership of the specified vJoy device for the calling process.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AcquireVJD(uint rID);

    /// <summary>Releases ownership of the specified vJoy device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void RelinquishVJD(uint rID);

    /// <summary>Returns the current status of the specified vJoy device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetVJDStatus(uint rID);

    // ── Reset functions ──────────────────────────────────────────────────────

    /// <summary>Resets all inputs on the specified vJoy device to their defaults.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ResetVJD(uint rID);

    /// <summary>Resets all inputs on all vJoy devices to their defaults.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ResetAll();

    /// <summary>Resets all buttons on the specified vJoy device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ResetButtons(uint rID);

    /// <summary>Resets all POV (hat) switches on the specified vJoy device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ResetPovs(uint rID);

    // ── Set values ───────────────────────────────────────────────────────────

    /// <summary>Sets the value of the specified axis on the vJoy device.</summary>
    /// <param name="value">Raw axis value (driver range, typically 0–32767).</param>
    /// <param name="rID">vJoy device ID (1-based).</param>
    /// <param name="axis">Axis code (<see cref="AxisCode"/>).</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetAxis(int value, uint rID, uint axis);

    /// <summary>Sets the pressed state of a button on the vJoy device.</summary>
    /// <param name="value"><c>true</c> = pressed, <c>false</c> = released.</param>
    /// <param name="rID">vJoy device ID (1-based).</param>
    /// <param name="nBtn">Button number (1-based).</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetBtn([MarshalAs(UnmanagedType.Bool)] bool value, uint rID, byte nBtn);

    /// <summary>Sets a discrete POV direction (0=N, 1=NE, 2=S, 3=W, -1=center).</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetDiscPov(int value, uint rID, byte nPov);

    /// <summary>Sets a continuous POV direction in hundredths of a degree (0–35999; -1=center).</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetContPov(uint value, uint rID, byte nPov);
}
