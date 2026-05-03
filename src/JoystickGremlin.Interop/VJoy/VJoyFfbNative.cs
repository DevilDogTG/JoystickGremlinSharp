// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace JoystickGremlin.Interop.VJoy;

/// <summary>
/// Delegate type for the vJoy force feedback general callback.
/// Called by the vJoy driver on a native thread when FFB data is available.
/// </summary>
/// <param name="ffbData">Pointer to the FFB packet data.</param>
/// <param name="context">User-supplied context pointer (the GCHandle of the source object).</param>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate void FfbGenCB(IntPtr ffbData, IntPtr context);

/// <summary>
/// Raw FFB packet data structure returned by the vJoy driver callback.
/// Matches the C struct FFB_DATA.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbData
{
    /// <summary>Size of this structure in bytes.</summary>
    public uint Size;

    /// <summary>Command / packet identifier.</summary>
    public uint Cmd;

    /// <summary>Pointer to the packet-specific payload data.</summary>
    public IntPtr Data;
}

/// <summary>
/// Force feedback packet type identifiers (FFBPType), as defined in the vJoy SDK.
/// </summary>
internal enum FfbPacketType : uint
{
    /// <summary>Set Effect Report.</summary>
    PT_EFFREP = 0x01,

    /// <summary>Set Envelope Report.</summary>
    PT_ENVREP = 0x02,

    /// <summary>Set Condition Report.</summary>
    PT_CONDREP = 0x03,

    /// <summary>Set Periodic Report.</summary>
    PT_PRIDREP = 0x04,

    /// <summary>Set Constant Force Report.</summary>
    PT_CONSTREP = 0x05,

    /// <summary>Set Ramp Force Report.</summary>
    PT_RAMPREP = 0x06,

    /// <summary>Custom Force Data Report.</summary>
    PT_CSTMREP = 0x07,

    /// <summary>Download Force Sample.</summary>
    PT_SMPLREP = 0x08,

    /// <summary>Effect Operation Report.</summary>
    PT_EFOPREP = 0x0A,

    /// <summary>Block Free Report.</summary>
    PT_BLKFRREP = 0x0B,

    /// <summary>Device Control.</summary>
    PT_CTRLREP = 0x0C,

    /// <summary>Device Gain Report.</summary>
    PT_GAINREP = 0x0D,

    /// <summary>Set Custom Force Report.</summary>
    PT_SETCREP = 0x0E,

    /// <summary>Create New Effect.</summary>
    PT_NEWEFREP = 0x01 + 0x10,

    /// <summary>Block Load Report.</summary>
    PT_BLKLDREP = 0x02 + 0x10,

    /// <summary>Pool Report.</summary>
    PT_POOLREP = 0x03 + 0x10,

    /// <summary>State Report.</summary>
    PT_STATEREP = 0x04 + 0x10,
}

/// <summary>
/// Native struct for the Set Effect Report packet (FFB_EFF_REPORT).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbEffReport
{
    /// <summary>Effect block index (1-based slot).</summary>
    public byte EBI;

    /// <summary>Effect type (maps to <see cref="JoystickGremlin.Core.ForceFeedback.FfbEffectType"/>).</summary>
    public uint EffectType;

    /// <summary>Duration in milliseconds.</summary>
    public ushort Duration;

    /// <summary>Trigger repeat interval in milliseconds.</summary>
    public ushort TriggerRpt;

    /// <summary>Sample period in milliseconds.</summary>
    public ushort SamplePrd;

    /// <summary>Start delay in milliseconds.</summary>
    public ushort StartDelay;

    /// <summary>Effect gain (0–255).</summary>
    public byte Gain;

    /// <summary>Trigger button index.</summary>
    public byte TriggerBtn;

    /// <summary>Axes enabled / direction byte.</summary>
    public byte AxesEnabledDir;

    /// <summary>Non-zero if direction is polar.</summary>
    public int Polar;

    /// <summary>Direction value (polar angle or X component in Cartesian).</summary>
    public ushort DirectionOrX;

    /// <summary>Y direction component (Cartesian only).</summary>
    public ushort DirY;
}

/// <summary>
/// Native struct for the Set Constant Force packet (FFB_EFF_CONSTANT).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbConstant
{
    /// <summary>Effect block index.</summary>
    public byte EBI;

    /// <summary>Magnitude (-10000 to +10000).</summary>
    public int Magnitude;
}

/// <summary>
/// Native struct for the Set Ramp Force packet.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbRamp
{
    /// <summary>Effect block index.</summary>
    public byte EBI;

    /// <summary>Starting magnitude (-10000 to +10000).</summary>
    public int Start;

    /// <summary>Ending magnitude (-10000 to +10000).</summary>
    public int End;
}

/// <summary>
/// Native struct for the Set Periodic packet (sine, square, triangle, sawtooth).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbPeriod
{
    /// <summary>Effect block index.</summary>
    public byte EBI;

    /// <summary>Peak magnitude (0–10000).</summary>
    public uint Magnitude;

    /// <summary>Bias offset.</summary>
    public int Offset;

    /// <summary>Phase offset in hundredths of a degree (0–35999).</summary>
    public uint Phase;

    /// <summary>Waveform period in microseconds.</summary>
    public uint Period;
}

/// <summary>
/// Native struct for the Set Condition packet (spring, damper, inertia, friction).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbCondition
{
    /// <summary>Effect block index.</summary>
    public byte EBI;

    /// <summary>Non-zero if this condition applies to the Y axis; zero for X axis.</summary>
    public int IsY;

    /// <summary>Center point offset.</summary>
    public int CenterPointOffset;

    /// <summary>Positive coefficient.</summary>
    public int PosCoeff;

    /// <summary>Negative coefficient.</summary>
    public int NegCoeff;

    /// <summary>Positive saturation value.</summary>
    public uint PosSatur;

    /// <summary>Negative saturation value.</summary>
    public uint NegSatur;

    /// <summary>Dead band around the center point.</summary>
    public int DeadBand;
}

/// <summary>
/// Native struct for the Set Envelope packet.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbEnvelope
{
    /// <summary>Effect block index.</summary>
    public byte EBI;

    /// <summary>Attack level.</summary>
    public uint AttackLevel;

    /// <summary>Fade level.</summary>
    public uint FadeLevel;

    /// <summary>Attack time in milliseconds.</summary>
    public uint AttackTime;

    /// <summary>Fade time in milliseconds.</summary>
    public uint FadeTime;
}

/// <summary>
/// Native struct for the Effect Operation packet.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VJoyFfbEffOp
{
    /// <summary>Effect block index.</summary>
    public byte EBI;

    /// <summary>Effect operation (maps to <see cref="JoystickGremlin.Core.ForceFeedback.FfbOperation"/>).</summary>
    public uint EffectOp;

    /// <summary>Loop count (255 = infinite).</summary>
    public byte LoopCount;
}

/// <summary>
/// Force feedback P/Invoke declarations for vJoyInterface.dll.
/// </summary>
internal static partial class VJoyFfbNative
{
    private const string DllName = "vJoyInterface.dll";

    /// <summary>Registers a general-purpose FFB callback with the vJoy driver.</summary>
    /// <param name="cb">Callback delegate (must be kept alive for the lifetime of registration).</param>
    /// <param name="data">User-supplied context pointer passed back to the callback.</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FfbRegisterGenCB(FfbGenCB cb, IntPtr data);

    /// <summary>Determines whether the vJoy driver supports force feedback.</summary>
    /// <param name="supported">Set to <c>true</c> if FFB is supported.</param>
    /// <returns><c>true</c> on success.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool vJoyFfbCap([MarshalAs(UnmanagedType.Bool)] out bool supported);

    /// <summary>Returns whether a specific vJoy device has force feedback configured.</summary>
    /// <param name="rID">vJoy device ID (1-based).</param>
    /// <returns><c>true</c> if the device has FFB configured.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsDeviceFfb(uint rID);

    /// <summary>Returns whether a specific FFB effect type is configured on the device.</summary>
    /// <param name="rID">vJoy device ID (1-based).</param>
    /// <param name="effect">Effect type value.</param>
    /// <returns><c>true</c> if the specified effect is supported.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsDeviceFfbEffect(uint rID, uint effect);

    /// <summary>Extracts the vJoy device ID from an FFB packet pointer.</summary>
    /// <param name="packet">Pointer to the raw FFB packet.</param>
    /// <param name="deviceId">Receives the device ID.</param>
    /// <returns>0 on success, non-zero on error.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_DeviceID(IntPtr packet, out uint deviceId);

    /// <summary>Extracts the packet type from an FFB packet pointer.</summary>
    /// <param name="packet">Pointer to the raw FFB packet.</param>
    /// <param name="type">Receives the packet type.</param>
    /// <returns>0 on success, non-zero on error.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Type(IntPtr packet, out FfbPacketType type);

    /// <summary>Decodes a Set Effect Report packet into a native struct pointer.</summary>
    /// <param name="packet">Pointer to the raw FFB packet.</param>
    /// <param name="effect">Pointer to a <see cref="VJoyFfbEffReport"/> to receive the data.</param>
    /// <returns>0 on success.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Eff_Report(IntPtr packet, IntPtr effect);

    /// <summary>Decodes a Set Constant Force packet.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Eff_Const(IntPtr packet, IntPtr effect);

    /// <summary>Decodes a Set Ramp Force packet.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Eff_Ramp(IntPtr packet, IntPtr effect);

    /// <summary>Decodes an Effect Operation packet.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_EffOp(IntPtr packet, IntPtr effect);

    /// <summary>Decodes a Device Control packet.</summary>
    /// <param name="packet">Pointer to the raw FFB packet.</param>
    /// <param name="control">Receives the device control command value.</param>
    /// <returns>0 on success.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_DevCtrl(IntPtr packet, out uint control);

    /// <summary>Decodes a Set Periodic packet.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Eff_Period(IntPtr packet, IntPtr effect);

    /// <summary>Decodes a Set Condition packet.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Eff_Cond(IntPtr packet, IntPtr effect);

    /// <summary>Decodes a Set Envelope packet.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Eff_Envlp(IntPtr packet, IntPtr effect);

    /// <summary>Decodes a Device Gain packet.</summary>
    /// <param name="packet">Pointer to the raw FFB packet.</param>
    /// <param name="gain">Receives the gain value (0–255).</param>
    /// <returns>0 on success.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Ffb_h_Eff_Gain(IntPtr packet, out byte gain);
}
