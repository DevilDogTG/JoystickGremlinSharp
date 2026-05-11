// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Interop.HidHide;

namespace JoystickGremlin.Core.Tests.Interop;

public sealed class HidHideCliParserTests
{
    [Fact]
    public void ParseDeviceList_HandlesEmptyOutput()
    {
        HidHideCliService.ParseDeviceList(string.Empty).Should().BeEmpty();
        HidHideCliService.ParseDeviceList("   \n  ").Should().BeEmpty();
    }

    [Fact]
    public void ParseDeviceList_DetectsHiddenFlag()
    {
        var output = """
            HID\VID_046D&PID_C24F\6&1abc [hidden]
            HID\VID_044F&PID_B66D\7&2def [visible]
            """;

        var devices = HidHideCliService.ParseDeviceList(output);

        devices.Should().HaveCount(2);
        devices[0].InstancePath.Should().Be(@"HID\VID_046D&PID_C24F\6&1abc");
        devices[0].IsHidden.Should().BeTrue();
        devices[1].IsHidden.Should().BeFalse();
    }

    [Fact]
    public void ParseDeviceList_SplitsFriendlyNameOnPipe()
    {
        var output = "HID\\VID_046D&PID_C24F\\6&1abc | Logitech G29 Driving Force [hidden]";

        var devices = HidHideCliService.ParseDeviceList(output);

        devices.Should().ContainSingle();
        devices[0].InstancePath.Should().Be(@"HID\VID_046D&PID_C24F\6&1abc");
        devices[0].DisplayName.Should().Be("Logitech G29 Driving Force");
        devices[0].IsHidden.Should().BeTrue();
    }

    [Fact]
    public void ParseDeviceList_SkipsCommentLines()
    {
        var output = """
            # this is a comment
            HID\VID_1234&PID_BEAD\X
            """;

        HidHideCliService.ParseDeviceList(output).Should().ContainSingle();
    }

    [Fact]
    public void ParseWhitelist_HandlesEmptyOutput()
    {
        HidHideCliService.ParseWhitelist(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void ParseWhitelist_ReturnsOneEntryPerLine()
    {
        var output = """
            C:\Games\Forza Horizon 5\ForzaHorizon5.exe
            C:\Games\AC Competizione\AC2-Win64-Shipping.exe
            """;

        var entries = HidHideCliService.ParseWhitelist(output);

        entries.Should().HaveCount(2);
        entries[0].ImagePath.Should().EndWith("ForzaHorizon5.exe");
    }
}
