// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Interop.VJoy;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.Tests.Interop.VJoy;

public sealed class VJoyFfbSourceTests
{
    [Fact]
    public void VJoyDeviceId_UsesLatestConfiguredSettingBeforeStart()
    {
        var settings = new AppSettings { FfbVJoyDeviceId = 1 };
        var settingsServiceMock = new Mock<ISettingsService>();
        settingsServiceMock.SetupGet(s => s.Settings).Returns(settings);

        var source = new VJoyFfbSource(
            settingsServiceMock.Object,
            Mock.Of<ILogger<VJoyFfbSource>>());

        settings.FfbVJoyDeviceId = 4;

        source.VJoyDeviceId.Should().Be(4);

        source.Dispose();
    }
}
