// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Devices.Backends;
using JoystickGremlin.Core.Exceptions;
using JoystickGremlin.Interop.JgsWheel;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Interop;

/// <summary>
/// The JGS Wheel driver is not yet built, so the manager must report NotInstalled
/// gracefully and never crash the host on acquire/release.
/// </summary>
public sealed class JgsWheelDeviceManagerTests
{
    private static JgsWheelDeviceManager CreateSut() =>
        new(NullLogger<JgsWheelDeviceManager>.Instance);

    [Fact]
    public void IsAvailable_WithoutDriver_ReturnsFalse()
    {
        using var sut = CreateSut();

        sut.IsAvailable.Should().BeFalse(
            "the driver service is not registered on the build/CI machine — manager must degrade safely");
    }

    [Fact]
    public void GetAvailableDeviceIds_WithoutDriver_ReturnsEmpty()
    {
        using var sut = CreateSut();
        sut.GetAvailableDeviceIds().Should().BeEmpty();
    }

    [Fact]
    public void GetStatus_WithoutDriver_ReturnsMissing()
    {
        using var sut = CreateSut();
        sut.GetStatus(1).Should().Be(VirtualDeviceStatus.Missing);
    }

    [Fact]
    public void AcquireDevice_WithoutDriver_ThrowsWithBuildHint()
    {
        using var sut = CreateSut();

        var act = () => sut.AcquireDevice(1);

        act.Should().Throw<VJoyException>()
            .WithMessage("*installer/wheel-driver/README.md*");
    }

    [Fact]
    public void GetDevice_WithoutDriver_ThrowsWithBuildHint()
    {
        using var sut = CreateSut();

        var act = () => sut.GetDevice(1);

        act.Should().Throw<VJoyException>()
            .WithMessage("*driver*");
    }

    [Fact]
    public void ReleaseDevice_WithoutDriver_DoesNotThrow()
    {
        using var sut = CreateSut();

        var act = () => sut.ReleaseDevice(1);

        act.Should().NotThrow("release on shutdown must be safe even when nothing was acquired");
    }

    [Fact]
    public void ReleaseAll_AndDispose_AreIdempotent()
    {
        var sut = CreateSut();

        sut.ReleaseAll();
        sut.Dispose();
        sut.Dispose();
    }
}

public sealed class JgsWheelBackendTests
{
    private static JgsWheelBackend CreateSut() =>
        new(new JgsWheelDeviceManager(NullLogger<JgsWheelDeviceManager>.Instance));

    [Fact]
    public void Metadata_HasStableIdAndRacingWheelKind()
    {
        var sut = CreateSut();

        sut.Id.Should().Be("jgs-wheel");
        sut.DisplayName.Should().Be("JGS Wheel");
        sut.Kind.Should().Be(BackendKind.RacingWheel);
        sut.Capabilities.SupportsForceFeedback.Should().BeTrue();
        sut.Capabilities.SupportsIdentitySpoofing.Should().BeTrue();
    }

    [Fact]
    public void Status_WithoutDriver_ReportsNotInstalled()
    {
        var sut = CreateSut();

        sut.Status.Should().Be(BackendStatus.NotInstalled);
    }

    [Fact]
    public void Constructor_NullManager_Throws()
    {
        var act = () => new JgsWheelBackend(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

public sealed class JgsWheelPrerequisiteCheckerTests
{
    [Fact]
    public void Check_OnCleanBuildMachine_ReturnsNotInstalled()
    {
        var result = JgsWheelPrerequisiteChecker.Check();

        // The build/CI machine doesn't have jgswheel installed.
        result.IsInstalled.Should().BeFalse();
        result.IsOk.Should().BeFalse();
    }

    [Fact]
    public void ServiceNameAndKeyPath_AreStable()
    {
        JgsWheelPrerequisiteChecker.ServiceName.Should().Be("jgswheel");
        JgsWheelPrerequisiteChecker.ParametersKeyPath.Should()
            .Be(@"SYSTEM\CurrentControlSet\Services\jgswheel\Parameters");
        JgsWheelPrerequisiteChecker.InterfaceDllName.Should().Be("JgsWheelInterface.dll");
    }
}
