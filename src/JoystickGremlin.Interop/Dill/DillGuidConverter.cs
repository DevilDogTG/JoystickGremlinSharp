// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;

namespace JoystickGremlin.Interop.Dill;

/// <summary>
/// Converts between the .NET <see cref="Guid"/> type and the native <see cref="NativeGuid"/> struct
/// used by the DILL P/Invoke layer.
/// </summary>
internal static class DillGuidConverter
{
    /// <summary>
    /// Converts a <see cref="NativeGuid"/> received from DILL into a .NET <see cref="Guid"/>.
    /// </summary>
    internal static Guid ToGuid(NativeGuid native)
    {
        // Guid(uint a, ushort b, ushort c, byte d, ...) maps directly to the GUID fields.
        var d = native.Data4 ?? new byte[8];
        return new Guid(
            native.Data1,
            native.Data2,
            native.Data3,
            d[0], d[1], d[2], d[3], d[4], d[5], d[6], d[7]);
    }

    /// <summary>
    /// Converts a .NET <see cref="Guid"/> into a <see cref="NativeGuid"/> for DILL P/Invoke calls.
    /// </summary>
    internal static NativeGuid FromGuid(Guid guid)
    {
        // Guid.ToByteArray() returns: Data1 (4 LE bytes) + Data2 (2 LE) + Data3 (2 LE) + Data4 (8 bytes)
        byte[] bytes = guid.ToByteArray();
        return new NativeGuid
        {
            Data1 = BitConverter.ToUInt32(bytes, 0),
            Data2 = BitConverter.ToUInt16(bytes, 4),
            Data3 = BitConverter.ToUInt16(bytes, 6),
            Data4 = bytes[8..16]
        };
    }
}
