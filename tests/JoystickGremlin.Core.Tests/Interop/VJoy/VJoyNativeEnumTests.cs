// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Interop.VJoy;

namespace JoystickGremlin.Core.Tests.Interop.VJoy;

/// <summary>
/// Verifies enum values in the vJoy native layer match the vJoy SDK constants.
/// </summary>
public class VJoyNativeEnumTests
{
    [Theory]
    [InlineData(AxisCode.X, 0x30u)]
    [InlineData(AxisCode.Y, 0x31u)]
    [InlineData(AxisCode.Z, 0x32u)]
    [InlineData(AxisCode.RX, 0x33u)]
    [InlineData(AxisCode.RY, 0x34u)]
    [InlineData(AxisCode.RZ, 0x35u)]
    [InlineData(AxisCode.SL0, 0x36u)]
    [InlineData(AxisCode.SL1, 0x37u)]
    public void AxisCode_Values_MatchVJoySdkConstants(AxisCode code, uint expected)
    {
        ((uint)code).Should().Be(expected);
    }

    [Theory]
    [InlineData(VjdStatus.Owned, 0)]
    [InlineData(VjdStatus.Free, 1)]
    [InlineData(VjdStatus.Busy, 2)]
    [InlineData(VjdStatus.Missing, 3)]
    [InlineData(VjdStatus.Unknown, 4)]
    public void VjdStatus_Values_MatchVJoySdkConstants(VjdStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Theory]
    [InlineData(HatType.Discrete, 0)]
    [InlineData(HatType.Continuous, 1)]
    public void HatType_Values_AreCorrect(HatType hatType, int expected)
    {
        ((int)hatType).Should().Be(expected);
    }
}
