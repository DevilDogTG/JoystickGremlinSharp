// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.HidHide;

namespace JoystickGremlin.Core.Tests.HidHideTests;

public sealed class NullHidHideServiceTests
{
    private readonly NullHidHideService _sut = new();

    [Fact]
    public void GetStatus_ReportsNotInstalled()
    {
        var status = _sut.GetStatus();
        status.IsInstalled.Should().BeFalse();
        status.CliPath.Should().BeNull();
    }

    [Fact]
    public async Task ListDevicesAsync_ReturnsEmpty()
    {
        var devices = await _sut.ListDevicesAsync();
        devices.Should().BeEmpty();
    }

    [Fact]
    public async Task MutatingOps_AreSafeNoOps()
    {
        await _sut.HideDeviceAsync("HID\\foo");
        await _sut.UnhideDeviceAsync("HID\\foo");
        await _sut.AddWhitelistEntryAsync(@"C:\game.exe");
        await _sut.RemoveWhitelistEntryAsync(@"C:\game.exe");
        await _sut.SetCloakEnabledAsync(true);
        await _sut.SetCloakEnabledAsync(false);
        (await _sut.ListWhitelistAsync()).Should().BeEmpty();
    }
}
