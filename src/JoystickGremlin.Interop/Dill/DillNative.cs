// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace JoystickGremlin.Interop.Dill;

/// <summary>
/// Native GUID structure matching the Windows GUID / DirectInput GUID layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeGuid
{
    /// <summary>First 32-bit component.</summary>
    public uint Data1;
    /// <summary>Second 16-bit component.</summary>
    public ushort Data2;
    /// <summary>Third 16-bit component.</summary>
    public ushort Data3;
    /// <summary>Last 8 bytes.</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Data4;
}

/// <summary>
/// Native structure for a single joystick input event received from DILL.
/// Matches the C layout: 16-byte GUID + 2 type/index bytes + 2 padding + 4-byte value = 24 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeJoystickInputData
{
    /// <summary>GUID of the device that produced the event.</summary>
    public NativeGuid DeviceGuid;
    /// <summary>Input type: 1=Axis, 2=Button, 3=Hat.</summary>
    public byte InputType;
    /// <summary>Zero-based index of the input.</summary>
    public byte InputIndex;
    // 2 bytes of implicit padding to 4-byte align Value.
    private readonly ushort _pad;
    /// <summary>New value of the input.</summary>
    public int Value;
}

/// <summary>
/// Native structure representing a single axis mapping entry.
/// Maps a zero-based linear axis index to the DirectInput axis identifier.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeAxisMap
{
    /// <summary>Sequential (linear) axis index.</summary>
    public uint LinearIndex;
    /// <summary>DirectInput axis identifier.</summary>
    public uint AxisIndex;
}

/// <summary>
/// Native structure summarising a single physical device reported by DILL.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct NativeDeviceSummary
{
    /// <summary>DirectInput GUID of the device.</summary>
    public NativeGuid DeviceGuid;
    /// <summary>USB vendor identifier.</summary>
    public uint VendorId;
    /// <summary>USB product identifier.</summary>
    public uint ProductId;
    /// <summary>DirectInput joystick identifier.</summary>
    public uint JoystickId;
    /// <summary>Human-readable device name (ANSI, MAX_PATH = 260 chars).</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string Name;
    /// <summary>Number of axes on the device.</summary>
    public uint AxisCount;
    /// <summary>Number of buttons on the device.</summary>
    public uint ButtonCount;
    /// <summary>Number of hats on the device.</summary>
    public uint HatCount;
    /// <summary>Axis map entries (up to 8).</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public NativeAxisMap[] AxisMap;
}

/// <summary>
/// Delegate type for DILL input event callbacks.
/// Called by the DILL library (cdecl) whenever an input changes state.
/// </summary>
/// <param name="data">The input event data.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void InputEventCallback(NativeJoystickInputData data);

/// <summary>
/// Delegate type for DILL device change callbacks.
/// Called by the DILL library (cdecl) when a device is connected or disconnected.
/// </summary>
/// <param name="data">Summary of the device that changed.</param>
/// <param name="actionType">1 = connected, 2 = disconnected.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void DeviceChangeCallback(NativeDeviceSummary data, byte actionType);

/// <summary>
/// Raw P/Invoke declarations for the DILL (Direct Input Listener Library) native DLL.
/// All functions use cdecl calling convention.
/// </summary>
internal static partial class DillNative
{
    private const string DllName = "dill.dll";

    /// <summary>Initialises the DILL library. Must be called once before any other DILL call.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void init();

    /// <summary>Registers the callback that receives all joystick input events.</summary>
    /// <param name="callback">Delegate to invoke on each input event. Caller must keep a reference alive.</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void set_input_event_callback(InputEventCallback callback);

    /// <summary>Registers the callback that receives device connect/disconnect notifications.</summary>
    /// <param name="callback">Delegate to invoke on device change. Caller must keep a reference alive.</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void set_device_change_callback(DeviceChangeCallback callback);

    /// <summary>Returns the number of DirectInput devices currently connected.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint get_device_count();

    /// <summary>Returns a device summary for the device at the given sequential index.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeDeviceSummary get_device_information_by_index(uint index);

    /// <summary>Returns a device summary for the device with the given GUID.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeDeviceSummary get_device_information_by_guid(NativeGuid guid);

    /// <summary>Returns whether a device with the given GUID is currently connected.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool device_exists(NativeGuid guid);

    /// <summary>Returns the current value of the specified axis on the given device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int get_axis(NativeGuid guid, uint axisIndex);

    /// <summary>Returns whether the specified button on the given device is currently pressed.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool get_button(NativeGuid guid, uint buttonIndex);

    /// <summary>Returns the current hat position value for the specified hat on the given device.</summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int get_hat(NativeGuid guid, uint hatIndex);
}
