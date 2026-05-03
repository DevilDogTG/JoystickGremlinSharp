// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace JoystickGremlin.Interop.DirectInput;

// ── P/Invoke declarations ─────────────────────────────────────────────────────

/// <summary>
/// Win32 and DirectInput 8 P/Invoke declarations used for force feedback output.
/// </summary>
internal static class DirectInputNative
{
    /// <summary>Creates a DirectInput8 interface object.</summary>
    [DllImport("dinput8.dll", CallingConvention = CallingConvention.Winapi)]
    internal static extern int DirectInput8Create(
        IntPtr hinst,
        uint dwVersion,
        in Guid riidltf,
        out IntPtr ppvOut,
        IntPtr punkOuter);

    /// <summary>Returns the module handle of the current process executable (or a loaded DLL when null).</summary>
    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <summary>Creates a window with extended style. Used to obtain a message-only HWND for SetCooperativeLevel.</summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    /// <summary>Destroys the specified window.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>DirectInput 8 version constant.</summary>
    internal const uint DIRECTINPUT_VERSION = 0x0800;

    /// <summary>Enumerate only force feedback capable devices.</summary>
    internal const uint DIEDFL_FORCEFEEDBACK = 0x00000100;

    /// <summary>Joystick device type (first byte of dwDevType).</summary>
    internal const uint DIDEVTYPE_JOYSTICK = 4;

    /// <summary>Exclusive access — prevents other applications from acquiring the device.</summary>
    internal const uint DISCL_EXCLUSIVE = 0x00000001;

    /// <summary>Non-exclusive access.</summary>
    internal const uint DISCL_NONEXCLUSIVE = 0x00000002;

    /// <summary>Foreground access — device is acquired only when the window has focus.</summary>
    internal const uint DISCL_FOREGROUND = 0x00000004;

    /// <summary>Background access — device remains acquired regardless of focus.</summary>
    internal const uint DISCL_BACKGROUND = 0x00000008;

    /// <summary>Effect direction specified in Cartesian coordinates.</summary>
    internal const uint DIEFF_CARTESIAN = 0x00000002;

    /// <summary>Axis offsets specified as object offsets.</summary>
    internal const uint DIEFF_OBJECTOFFSETS = 0x00000020;

    /// <summary>DIEP flag: type-specific parameters.</summary>
    internal const uint DIEP_TYPESPECIFICPARAMS = 0x00000100;

    /// <summary>DIEP flag: gain.</summary>
    internal const uint DIEP_GAIN = 0x00000004;

    /// <summary>DIEP flag: duration.</summary>
    internal const uint DIEP_DURATION = 0x00000001;

    /// <summary>DIEP flag: start effect immediately after SetParameters.</summary>
    internal const uint DIEP_START = 0x20000000;

    /// <summary>DIEP flag: sample period.</summary>
    internal const uint DIEP_SAMPLEPERIOD = 0x00000002;

    /// <summary>DIEP flag: axes.</summary>
    internal const uint DIEP_AXES = 0x00000020;

    /// <summary>DIEP flag: direction.</summary>
    internal const uint DIEP_DIRECTION = 0x00000040;

    /// <summary>DIEP flag: envelope.</summary>
    internal const uint DIEP_ENVELOPE = 0x00000080;

    /// <summary>DIEP flag: trigger button.</summary>
    internal const uint DIEP_TRIGGERBUTTON = 0x00000008;

    /// <summary>DIEP flag: trigger repeat interval.</summary>
    internal const uint DIEP_TRIGGERREPEATINTERVAL = 0x00000010;

    /// <summary>Nominal maximum force value for DirectInput force feedback.</summary>
    internal const uint DI_FFNOMINALMAX = 10000;

    /// <summary>No trigger button — effect plays immediately without a button press.</summary>
    internal const uint DIEB_NOTRIGGER = 0xFFFFFFFF;

    /// <summary>Duration value meaning the effect plays forever.</summary>
    internal const uint INFINITE_DURATION = 0xFFFFFFFF;

    /// <summary>GetForceFeedbackState flag: actuators are off.</summary>
    internal const uint DIGFFS_ACTUATORSOFF = 0x80000000;

    /// <summary>Data format flag: absolute axis values.</summary>
    internal const uint DIDF_ABSAXIS = 0x00000001;

    /// <summary>Object type flag: axis.</summary>
    internal const uint DIDFT_AXIS = 0x00000100;

    /// <summary>Object type flag: any instance.</summary>
    internal const uint DIDFT_ANYINSTANCE = 0x00FFFF00;

    /// <summary>SendForceFeedbackCommand flag: stop all effects.</summary>
    internal const uint DISFFC_STOPALL = 0x00000020;

    /// <summary>SendForceFeedbackCommand flag: reset the device.</summary>
    internal const uint DISFFC_RESET = 0x00000040;

    /// <summary>SendForceFeedbackCommand flag: enable actuators.</summary>
    internal const uint DISFFC_ACTUATORSE = 0x00000004;

    /// <summary>SendForceFeedbackCommand flag: disable actuators.</summary>
    internal const uint DISFFC_ACTUATORSD = 0x00000008;

    /// <summary>SendForceFeedbackCommand flag: pause all playing effects.</summary>
    internal const uint DISFFC_PAUSE = 0x00000002;

    /// <summary>SendForceFeedbackCommand flag: continue paused effects.</summary>
    internal const uint DISFFC_CONTINUE = 0x00000001;

    // ── GUIDs ─────────────────────────────────────────────────────────────────

    /// <summary>IID of IDirectInput8W.</summary>
    internal static readonly Guid IID_IDirectInput8W = new("BF798033-483A-4DA2-AA99-5D64ED369700");

    /// <summary>IID of IDirectInputDevice8W.</summary>
    internal static readonly Guid IID_IDirectInputDevice8W = new("54D41081-DC15-4833-A41B-748F73A38179");

    /// <summary>Effect GUID for Constant Force.</summary>
    internal static readonly Guid GUID_ConstantForce = new("13541C20-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Sawtooth Down.</summary>
    internal static readonly Guid GUID_SawtoothDown = new("13541C22-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Sawtooth Up.</summary>
    internal static readonly Guid GUID_SawtoothUp = new("13541C23-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Sine.</summary>
    internal static readonly Guid GUID_Sine = new("13541C24-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Spring.</summary>
    internal static readonly Guid GUID_Spring = new("13541C27-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Damper.</summary>
    internal static readonly Guid GUID_Damper = new("13541C21-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Inertia.</summary>
    internal static readonly Guid GUID_Inertia = new("13541C25-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Friction.</summary>
    internal static readonly Guid GUID_Friction = new("13541C26-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Triangle.</summary>
    internal static readonly Guid GUID_Triangle = new("13541C28-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>Effect GUID for Square.</summary>
    internal static readonly Guid GUID_Square = new("13541C29-8E33-11D0-9AD0-00A0C9A06E35");

    /// <summary>DirectInput GUID for the X axis (HID usage 0x30).</summary>
    internal static readonly Guid GUID_XAxis = new("A36D02E0-C9F3-11CF-BFC7-444553540000");

    // ── COM interface helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Maps a <see cref="JoystickGremlin.Core.ForceFeedback.FfbEffectType"/> to
    /// the corresponding DirectInput effect GUID.
    /// Returns <see cref="Guid.Empty"/> for unmapped types.
    /// </summary>
    internal static Guid GetEffectGuid(JoystickGremlin.Core.ForceFeedback.FfbEffectType type)
    {
        return type switch
        {
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.ConstantForce => GUID_ConstantForce,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.Spring        => GUID_Spring,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.Damper        => GUID_Damper,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.Inertia       => GUID_Inertia,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.Friction      => GUID_Friction,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.Sine          => GUID_Sine,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.Square        => GUID_Square,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.Triangle      => GUID_Triangle,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.SawtoothUp    => GUID_SawtoothUp,
            JoystickGremlin.Core.ForceFeedback.FfbEffectType.SawtoothDown  => GUID_SawtoothDown,
            _ => Guid.Empty,
        };
    }
}

// ── COM interfaces ────────────────────────────────────────────────────────────

/// <summary>
/// COM interface for IDirectInput8W. All vtable methods must appear in order.
/// </summary>
[ComImport]
[Guid("BF798033-483A-4DA2-AA99-5D64ED369700")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirectInput8W
{
    /// <summary>Creates a DirectInput device object.</summary>
    [PreserveSig]
    int CreateDevice(in Guid rguid, out IntPtr lplpDirectInputDevice, IntPtr pUnkOuter);

    /// <summary>Enumerates all installed and attached DirectInput devices.</summary>
    [PreserveSig]
    int EnumDevices(uint dwDevType, IntPtr lpCallback, IntPtr pvRef, uint dwFlags);

    /// <summary>Retrieves capabilities of the DirectInput interface (unused).</summary>
    [PreserveSig]
    int GetDevCaps_Unused();

    /// <summary>Runs the DirectInput control panel (unused).</summary>
    [PreserveSig]
    int RunControlPanel_Unused(IntPtr hwndOwner, uint dwFlags);

    /// <summary>Initialises the DirectInput object (called internally by DirectInput8Create — unused).</summary>
    [PreserveSig]
    int Initialize_Unused(IntPtr hinst, uint dwVersion);

    /// <summary>Finds a device by a format string and instance name (unused).</summary>
    [PreserveSig]
    int FindDevice_Unused(in Guid rguidClass, [MarshalAs(UnmanagedType.LPWStr)] string ptszName, out Guid pguidInstance);

    /// <summary>Enumerates devices by semantics (unused).</summary>
    [PreserveSig]
    int EnumDevicesBySemantics_Unused(
        [MarshalAs(UnmanagedType.LPWStr)] string? ptszUserName,
        IntPtr lpdiActionFormat,
        IntPtr lpCallback,
        IntPtr pvRef,
        uint dwFlags);

    /// <summary>Configures devices (unused).</summary>
    [PreserveSig]
    int ConfigureDevices_Unused(IntPtr lpdiCallback, IntPtr lpdiCDParams, uint dwFlags, IntPtr pvRef);
}

/// <summary>
/// COM interface for IDirectInputDevice8W. All 29 vtable methods must appear in order.
/// </summary>
[ComImport]
[Guid("54D41081-DC15-4833-A41B-748F73A38179")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirectInputDevice8W
{
    /// <summary>Retrieves device capabilities (unused).</summary>
    [PreserveSig]
    int GetCapabilities_Unused(IntPtr lpDIDevCaps);

    /// <summary>Enumerates device objects (unused).</summary>
    [PreserveSig]
    int EnumObjects_Unused(IntPtr lpCallback, IntPtr pvRef, uint dwFlags);

    /// <summary>Retrieves a device property (unused).</summary>
    [PreserveSig]
    int GetProperty_Unused(in Guid rguidProp, IntPtr pdiph);

    /// <summary>Sets a device property (unused).</summary>
    [PreserveSig]
    int SetProperty_Unused(in Guid rguidProp, IntPtr pdiph);

    /// <summary>Acquires the device, enabling input collection.</summary>
    [PreserveSig]
    int Acquire();

    /// <summary>Unacquires the device.</summary>
    [PreserveSig]
    int Unacquire();

    /// <summary>Retrieves device state (unused for FFB-only use).</summary>
    [PreserveSig]
    int GetDeviceState_Unused(uint cbData, IntPtr lpvData);

    /// <summary>Retrieves buffered input data (unused).</summary>
    [PreserveSig]
    int GetDeviceData_Unused(uint cbObjectData, IntPtr rgdod, ref uint pdwInOut, uint dwFlags);

    /// <summary>Sets the data format for the device.</summary>
    [PreserveSig]
    int SetDataFormat(IntPtr lpdf);

    /// <summary>Sets the event notification handle (unused).</summary>
    [PreserveSig]
    int SetEventNotification_Unused(IntPtr hEvent);

    /// <summary>Sets the cooperative level for the device.</summary>
    [PreserveSig]
    int SetCooperativeLevel(IntPtr hwnd, uint dwFlags);

    /// <summary>Retrieves information about a device object (unused).</summary>
    [PreserveSig]
    int GetObjectInfo_Unused(IntPtr pdidoi, uint dwObj, uint dwHow);

    /// <summary>Retrieves device information (unused).</summary>
    [PreserveSig]
    int GetDeviceInfo_Unused(IntPtr pdidi);

    /// <summary>Runs the device configuration dialog (unused).</summary>
    [PreserveSig]
    int RunControlPanel_Unused(IntPtr hwndOwner, uint dwFlags);

    /// <summary>Initialises the device (called internally — unused).</summary>
    [PreserveSig]
    int Initialize_Unused(IntPtr hinst, uint dwVersion, in Guid rguid);

    /// <summary>Creates a force feedback effect object.</summary>
    [PreserveSig]
    int CreateEffect(in Guid rguid, IntPtr lpeff, out IntPtr lplpDirectInputEffect, IntPtr pUnkOuter);

    /// <summary>Enumerates force feedback effects (unused).</summary>
    [PreserveSig]
    int EnumEffects_Unused(IntPtr lpCallback, IntPtr pvRef, uint dwEffType);

    /// <summary>Retrieves force feedback effect information (unused).</summary>
    [PreserveSig]
    int GetEffectInfo_Unused(IntPtr pdei, in Guid rguid);

    /// <summary>Retrieves the current force feedback state.</summary>
    [PreserveSig]
    int GetForceFeedbackState(out uint pdwOut);

    /// <summary>Sends a force feedback command to the device.</summary>
    [PreserveSig]
    int SendForceFeedbackCommand(uint dwFlags);

    /// <summary>Enumerates created force feedback effect objects (unused).</summary>
    [PreserveSig]
    int EnumCreatedEffectObjects_Unused(IntPtr lpCallback, IntPtr pvRef, uint fl);

    /// <summary>Escapes to device-specific functionality (unused).</summary>
    [PreserveSig]
    int Escape_Unused(IntPtr pdie);

    /// <summary>Polls the device for input data (unused for FFB-only use).</summary>
    [PreserveSig]
    int Poll_Unused();

    /// <summary>Sends device data (unused).</summary>
    [PreserveSig]
    int SendDeviceData_Unused(uint cbObjectData, IntPtr rgdod, ref uint pdwInOut, uint dwFlags);

    /// <summary>Enumerates effects in a file (unused).</summary>
    [PreserveSig]
    int EnumEffectsInFile_Unused([MarshalAs(UnmanagedType.LPWStr)] string lpszFileName, IntPtr pec, IntPtr pvRef, uint dwFlags);

    /// <summary>Writes effects to a file (unused).</summary>
    [PreserveSig]
    int WriteEffectToFile_Unused([MarshalAs(UnmanagedType.LPWStr)] string lpszFileName, uint dwEntries, IntPtr rgDiFileEft, uint dwFlags);

    /// <summary>Sets the action map (unused).</summary>
    [PreserveSig]
    int BuildActionMap_Unused(IntPtr lpdiaf, [MarshalAs(UnmanagedType.LPWStr)] string? lpszUserName, uint dwFlags);

    /// <summary>Sets the action map (unused).</summary>
    [PreserveSig]
    int SetActionMap_Unused(IntPtr lpdiaf, [MarshalAs(UnmanagedType.LPWStr)] string? lpszUserName, uint dwFlags);

    /// <summary>Retrieves the image of the device (unused).</summary>
    [PreserveSig]
    int GetImageInfo_Unused(IntPtr lpdiDevImageInfoHeader);
}

/// <summary>
/// COM interface for IDirectInputEffect. All 10 vtable methods must appear in order.
/// </summary>
[ComImport]
[Guid("E7E1F7C0-88D2-11D0-9AD0-00A0C9A06E35")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirectInputEffect
{
    /// <summary>Initialises the effect (called internally — unused directly).</summary>
    [PreserveSig]
    int Initialize_Unused(IntPtr hinst, uint dwVersion, in Guid rguid);

    /// <summary>Retrieves the GUID identifying the effect (unused).</summary>
    [PreserveSig]
    int GetEffectGuid_Unused(out Guid pguid);

    /// <summary>Retrieves the parameters of the effect (unused).</summary>
    [PreserveSig]
    int GetParameters_Unused(IntPtr lpeff, uint dwFlags);

    /// <summary>Sets the parameters of the effect.</summary>
    [PreserveSig]
    int SetParameters(IntPtr lpeff, uint dwFlags);

    /// <summary>Begins playing the effect.</summary>
    [PreserveSig]
    int Start(uint dwIterations, uint dwFlags);

    /// <summary>Stops playing the effect.</summary>
    [PreserveSig]
    int Stop();

    /// <summary>Retrieves the playback status of the effect (unused).</summary>
    [PreserveSig]
    int GetEffectStatus_Unused(out uint pdwFlags);

    /// <summary>Downloads the effect into the device (called implicitly by Start — unused directly).</summary>
    [PreserveSig]
    int Download_Unused();

    /// <summary>Removes the effect from the device.</summary>
    [PreserveSig]
    int Unload();

    /// <summary>Escapes to device-specific functionality (unused).</summary>
    [PreserveSig]
    int Escape_Unused(IntPtr pdie);
}

// ── Native structs ────────────────────────────────────────────────────────────

/// <summary>
/// Describes a DirectInput effect (DIEFFECT / DIEFFECT_DX6).
/// Size must be 80 bytes on x64 (DIEFFECT_DX6 layout).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 80)]
internal struct DiEffect
{
    /// <summary>Size of this structure (set to 80 = DIEFFECT_DX6).</summary>
    [FieldOffset(0)]  public uint dwSize;

    /// <summary>Flags controlling direction encoding and other behaviours.</summary>
    [FieldOffset(4)]  public uint dwFlags;

    /// <summary>Duration of the effect in microseconds.</summary>
    [FieldOffset(8)]  public uint dwDuration;

    /// <summary>Sample period in microseconds.</summary>
    [FieldOffset(12)] public uint dwSamplePeriod;

    /// <summary>Overall gain in the range 0–10000.</summary>
    [FieldOffset(16)] public uint dwGain;

    /// <summary>Trigger button, or DIEB_NOTRIGGER (0xFFFFFFFF).</summary>
    [FieldOffset(20)] public uint dwTriggerButton;

    /// <summary>Trigger repeat interval in microseconds.</summary>
    [FieldOffset(24)] public uint dwTriggerRepeatInterval;

    /// <summary>Number of axes the effect acts on.</summary>
    [FieldOffset(28)] public uint cAxes;

    /// <summary>Pointer to array of axis object offsets.</summary>
    [FieldOffset(32)] public IntPtr rgdwAxes;

    /// <summary>Pointer to array of direction values.</summary>
    [FieldOffset(40)] public IntPtr rglDirection;

    /// <summary>Pointer to a DIENVELOPE structure, or null.</summary>
    [FieldOffset(48)] public IntPtr lpEnvelope;

    /// <summary>Size of the type-specific parameters in bytes.</summary>
    [FieldOffset(56)] public uint cbTypeSpecificParams;

    // 4 bytes padding at offset 60 to align the next pointer field to 8 bytes on x64.

    /// <summary>Pointer to the type-specific parameters structure.</summary>
    [FieldOffset(64)] public IntPtr lpvTypeSpecificParams;

    /// <summary>Start delay in microseconds.</summary>
    [FieldOffset(72)] public uint dwStartDelay;
}

/// <summary>
/// Type-specific parameters for a constant force effect (DICONSTANTFORCE).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DiConstantForce
{
    /// <summary>Signed magnitude in the range -10000 to +10000.</summary>
    public int lMagnitude;
}

/// <summary>
/// Type-specific parameters for a periodic effect (DIPERIODIC).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DiPeriodic
{
    /// <summary>Peak magnitude (0–10000).</summary>
    public uint dwMagnitude;

    /// <summary>Bias offset applied to the waveform.</summary>
    public int lOffset;

    /// <summary>Phase in hundredths of a degree (0–35999).</summary>
    public uint dwPhase;

    /// <summary>Period of the waveform in microseconds.</summary>
    public uint dwPeriod;
}

/// <summary>
/// Type-specific parameters for a condition effect (DICONDITION — spring, damper, inertia, friction).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DiCondition
{
    /// <summary>Center point offset.</summary>
    public int lOffset;

    /// <summary>Positive coefficient.</summary>
    public int lPositiveCoefficient;

    /// <summary>Negative coefficient.</summary>
    public int lNegativeCoefficient;

    /// <summary>Positive saturation value.</summary>
    public uint dwPositiveSaturation;

    /// <summary>Negative saturation value.</summary>
    public uint dwNegativeSaturation;

    /// <summary>Dead band around the center point.</summary>
    public int lDeadBand;
}

/// <summary>
/// Envelope parameters for a force feedback effect (DIENVELOPE).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DiEnvelope
{
    /// <summary>Size of this structure (must be set to sizeof(DiEnvelope) = 20).</summary>
    public uint dwSize;

    /// <summary>Amplitude at the start of the attack phase.</summary>
    public uint dwAttackLevel;

    /// <summary>Duration of the attack phase in microseconds.</summary>
    public uint dwAttackTime;

    /// <summary>Amplitude at the end of the fade phase.</summary>
    public uint dwFadeLevel;

    /// <summary>Duration of the fade phase in microseconds.</summary>
    public uint dwFadeTime;
}

/// <summary>
/// Describes a single object within a DirectInput data format (DIOBJECTDATAFORMAT).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DiObjectDataFormat
{
    /// <summary>Pointer to a GUID identifying the object type, or null for any.</summary>
    public IntPtr pguid;

    /// <summary>Offset within the data packet for this object.</summary>
    public uint dwOfs;

    /// <summary>Object type and instance flags (DIDFT_*).</summary>
    public uint dwType;

    /// <summary>Object flags (DIDOI_*).</summary>
    public uint dwFlags;
}

/// <summary>
/// Describes the data format for a DirectInput device (DIDATAFORMAT).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DiDataFormat
{
    /// <summary>Size of this structure.</summary>
    public uint dwSize;

    /// <summary>Size of each <see cref="DiObjectDataFormat"/> element.</summary>
    public uint dwObjSize;

    /// <summary>Data format flags (DIDF_*).</summary>
    public uint dwFlags;

    /// <summary>Total size of the data packet in bytes.</summary>
    public uint dwDataSize;

    /// <summary>Number of object descriptors in <see cref="rgodf"/>.</summary>
    public uint dwNumObjs;

    /// <summary>Pointer to an array of <see cref="DiObjectDataFormat"/> structures.</summary>
    public IntPtr rgodf;
}

/// <summary>
/// Describes a DirectInput device instance as returned by EnumDevices (DIDEVICEINSTANCEW).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DiDeviceInstance
{
    /// <summary>Size of this structure.</summary>
    public uint dwSize;

    /// <summary>Instance GUID uniquely identifying this device instance.</summary>
    public Guid guidInstance;

    /// <summary>Product GUID encoding the USB VID/PID.</summary>
    public Guid guidProduct;

    /// <summary>Device type flags.</summary>
    public uint dwDevType;

    /// <summary>Human-readable instance name.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string tszInstanceName;

    /// <summary>Human-readable product name.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string tszProductName;

    /// <summary>GUID of the force feedback driver (if any).</summary>
    public Guid guidFFDriver;

    /// <summary>HID usage page.</summary>
    public ushort wUsagePage;

    /// <summary>HID usage.</summary>
    public ushort wUsage;
}
