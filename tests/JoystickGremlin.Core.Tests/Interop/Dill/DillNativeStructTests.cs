// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using JoystickGremlin.Interop.Dill;

namespace JoystickGremlin.Core.Tests.Interop.Dill;

/// <summary>
/// Verifies that the DILL native struct layouts match the expected C struct sizes.
/// These tests catch any inadvertent layout changes that would break P/Invoke marshalling.
/// </summary>
public class DillNativeStructTests
{
    [Fact]
    public void NativeGuid_Size_Is16Bytes()
    {
        Marshal.SizeOf<NativeGuid>().Should().Be(16);
    }

    [Fact]
    public void NativeJoystickInputData_Size_Is24Bytes()
    {
        Marshal.SizeOf<NativeJoystickInputData>().Should().Be(24);
    }

    [Fact]
    public void NativeAxisMap_Size_Is8Bytes()
    {
        Marshal.SizeOf<NativeAxisMap>().Should().Be(8);
    }

    [Fact]
    public void NativeDeviceSummary_Size_Is364Bytes()
    {
        // 16 (GUID) + 3×4 (vendor/product/joystick id) + 260 (name) + 3×4 (counts) + 8×8 (axis map) = 364
        Marshal.SizeOf<NativeDeviceSummary>().Should().Be(364);
    }
}
