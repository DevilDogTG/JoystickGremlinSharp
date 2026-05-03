// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using JoystickGremlin.Core.ForceFeedback;
using JoystickGremlin.Interop.DirectInput;

namespace JoystickGremlin.Interop.Moza;

/// <summary>
/// Enumerates MOZA force feedback wheels available via DirectInput 8.
/// </summary>
public static class MozaFfbDeviceEnumerator
{
    private const ushort MozaVendorId = 0x346e;

    /// <summary>
    /// Discovers all MOZA force feedback wheel devices connected to the system.
    /// </summary>
    /// <param name="cancellationToken">A token that may cancel the operation.</param>
    /// <returns>
    /// A read-only list of <see cref="FfbWheelInfo"/> records describing the discovered devices.
    /// Returns an empty list if no MOZA devices are found or if DirectInput is unavailable.
    /// </returns>
    public static Task<IReadOnlyList<FfbWheelInfo>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => DiscoverDevices(), cancellationToken);
    }

    private static IReadOnlyList<FfbWheelInfo> DiscoverDevices()
    {
        var results = new List<FfbWheelInfo>();

        IntPtr hInstance = DirectInputNative.GetModuleHandle(null);

        int hr = DirectInputNative.DirectInput8Create(
            hInstance,
            DirectInputNative.DIRECTINPUT_VERSION,
            DirectInputNative.IID_IDirectInput8W,
            out IntPtr di8Ptr,
            IntPtr.Zero);

        if (hr < 0 || di8Ptr == IntPtr.Zero)
        {
            return results;
        }

        IDirectInput8W di8 = (IDirectInput8W)Marshal.GetObjectForIUnknown(di8Ptr);
        Marshal.Release(di8Ptr);

        try
        {
            EnumDevicesCallback callback = (ref DiDeviceInstance instance, IntPtr _) =>
            {
                byte[] guidBytes = instance.guidProduct.ToByteArray();
                ushort vid = (ushort)(guidBytes[0] | (guidBytes[1] << 8));
                ushort pid = (ushort)(guidBytes[2] | (guidBytes[3] << 8));

                if (vid == MozaVendorId)
                {
                    results.Add(new FfbWheelInfo(
                        instance.guidInstance,
                        instance.guidProduct,
                        instance.tszInstanceName ?? string.Empty,
                        instance.tszProductName ?? string.Empty,
                        vid,
                        pid));
                }

                return 1; // DIENUM_CONTINUE
            };

            GCHandle callbackHandle = GCHandle.Alloc(callback);
            IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(callback);
            try
            {
                di8.EnumDevices(
                    DirectInputNative.DIDEVTYPE_JOYSTICK,
                    callbackPtr,
                    IntPtr.Zero,
                    DirectInputNative.DIEDFL_FORCEFEEDBACK);
            }
            finally
            {
                callbackHandle.Free();
            }
        }
        finally
        {
            Marshal.ReleaseComObject(di8);
        }

        return results;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumDevicesCallback(ref DiDeviceInstance lpddi, IntPtr pvRef);
}
