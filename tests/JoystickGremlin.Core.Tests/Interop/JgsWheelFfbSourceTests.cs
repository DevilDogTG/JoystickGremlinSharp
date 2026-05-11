// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.ForceFeedback;
using JoystickGremlin.Interop.JgsWheel;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Interop;

public sealed class JgsWheelFfbSourceTests
{
    private static JgsWheelFfbSource CreateSut() =>
        new(NullLogger<JgsWheelFfbSource>.Instance);

    [Fact]
    public void IsFfbCapable_WithoutDriver_ReturnsFalse()
    {
        using var sut = CreateSut();
        sut.IsFfbCapable.Should().BeFalse();
    }

    [Fact]
    public void Start_WithoutDriver_DoesNotMarkRunning()
    {
        using var sut = CreateSut();
        sut.Start();
        sut.IsRunning.Should().BeFalse(
            "without the driver, Start logs a warning and stays idle so the bridge can fall back");
    }

    [Fact]
    public void StartStop_AreIdempotent()
    {
        using var sut = CreateSut();
        sut.Stop();
        sut.Start();
        sut.Start();
        sut.Stop();
        sut.Stop();
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        var sut = CreateSut();
        sut.Dispose();
        Action act = () => sut.Start();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var sut = CreateSut();
        sut.Dispose();
        sut.Dispose();
    }
}
