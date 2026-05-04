// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using FluentAssertions;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class HatToAxisFunctorTests
{
    private readonly Mock<IVirtualDeviceManager> _managerMock = new();
    private readonly Mock<IVirtualDevice> _deviceMock = new();
    private readonly Mock<ILogger<HatToAxisDescriptor>> _loggerMock = new();
    private readonly HatToAxisDescriptor _descriptor;

    public HatToAxisFunctorTests()
    {
        _managerMock
            .Setup(m => m.AcquireDevice(It.IsAny<uint>()))
            .Returns(_deviceMock.Object);

        _descriptor = new HatToAxisDescriptor(_managerMock.Object, _loggerMock.Object);
    }

    private static InputEvent HatEvent(double degrees) =>
        new(InputType.JoystickHat, Guid.Empty, 1, degrees);

    private static JsonObject DefaultConfig() =>
        new() { ["vjoyId"] = 1, ["xAxisIndex"] = 1, ["yAxisIndex"] = 2 };

    // ── ComputeAxisValues (unit tests, no I/O) ────────────────────────────────

    [Fact]
    public void ComputeAxisValues_Center_ReturnsZeroZero()
    {
        var (x, y) = HatToAxisDescriptor_ComputeAxisValues(-1);

        x.Should().Be(0.0);
        y.Should().Be(0.0);
    }

    [Theory]
    [InlineData(0,   0.0,    1.0)]    // North
    [InlineData(90,  1.0,    0.0)]    // East
    [InlineData(180, 0.0,   -1.0)]    // South
    [InlineData(270, -1.0,   0.0)]    // West
    public void ComputeAxisValues_Cardinals_AreCorrect(double degrees, double expectedX, double expectedY)
    {
        var (x, y) = HatToAxisDescriptor_ComputeAxisValues(degrees);

        x.Should().BeApproximately(expectedX, precision: 0.0001);
        y.Should().BeApproximately(expectedY, precision: 0.0001);
    }

    [Theory]
    [InlineData(45,  0.7071,  0.7071)]   // NE
    [InlineData(135, 0.7071, -0.7071)]   // SE
    [InlineData(225,-0.7071, -0.7071)]   // SW
    [InlineData(315,-0.7071,  0.7071)]   // NW
    public void ComputeAxisValues_Diagonals_AreApproximatelySqrtHalf(double degrees, double expectedX, double expectedY)
    {
        var (x, y) = HatToAxisDescriptor_ComputeAxisValues(degrees);

        x.Should().BeApproximately(expectedX, precision: 0.001);
        y.Should().BeApproximately(expectedY, precision: 0.001);
    }

    [Fact]
    public void ComputeAxisValues_CentidegreeNorth_ReturnsYPlus1()
    {
        // Hat POV from vJoy uses centidegrees (0 = 0°, 9000 = 90°)
        var (x, y) = HatToAxisDescriptor_ComputeAxisValues(0);

        x.Should().BeApproximately(0.0, precision: 0.0001);
        y.Should().BeApproximately(1.0, precision: 0.0001);
    }

    [Fact]
    public void ComputeAxisValues_CentidegreeEast_ReturnsXPlus1()
    {
        var (x, y) = HatToAxisDescriptor_ComputeAxisValues(9000);

        x.Should().BeApproximately(1.0, precision: 0.0001);
        y.Should().BeApproximately(0.0, precision: 0.0001);
    }

    // ── Functor integration tests ─────────────────────────────────────────────

    [Fact]
    public async Task Functor_Center_SetsBothAxesToZero()
    {
        var functor = _descriptor.CreateFunctor(DefaultConfig());

        await functor.ExecuteAsync(HatEvent(-1));

        _deviceMock.Verify(d => d.SetAxis(1, 0.0), Times.Once);
        _deviceMock.Verify(d => d.SetAxis(2, 0.0), Times.Once);
    }

    [Fact]
    public async Task Functor_North_SetsYPlusOne_XZero()
    {
        var functor = _descriptor.CreateFunctor(DefaultConfig());

        await functor.ExecuteAsync(HatEvent(0));

        _deviceMock.Verify(d => d.SetAxis(1, It.Is<double>(v => Math.Abs(v) < 0.0001)), Times.Once);
        _deviceMock.Verify(d => d.SetAxis(2, It.Is<double>(v => Math.Abs(v - 1.0) < 0.0001)), Times.Once);
    }

    [Fact]
    public async Task Functor_East_SetsXPlusOne_YZero()
    {
        var functor = _descriptor.CreateFunctor(DefaultConfig());

        await functor.ExecuteAsync(HatEvent(90));

        _deviceMock.Verify(d => d.SetAxis(1, It.Is<double>(v => Math.Abs(v - 1.0) < 0.0001)), Times.Once);
        _deviceMock.Verify(d => d.SetAxis(2, It.Is<double>(v => Math.Abs(v) < 0.0001)), Times.Once);
    }

    [Fact]
    public async Task Functor_NullConfig_UsesDefaults()
    {
        var functor = _descriptor.CreateFunctor(null);

        // Center hat → both axes 0
        await functor.ExecuteAsync(HatEvent(-1));

        // Should use vjoyId=1, xAxisIndex=1, yAxisIndex=2
        _deviceMock.Verify(d => d.SetAxis(1, 0.0), Times.Once);
        _deviceMock.Verify(d => d.SetAxis(2, 0.0), Times.Once);
    }

    [Fact]
    public async Task Functor_XAxisIndexZero_DoesNotSetXAxis()
    {
        var config = new JsonObject { ["vjoyId"] = 1, ["xAxisIndex"] = 0, ["yAxisIndex"] = 2 };
        var functor = _descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(HatEvent(0));

        // xAxisIndex=0 → X axis write skipped; Y axis should get +1
        _deviceMock.Verify(d => d.SetAxis(0, It.IsAny<double>()), Times.Never);
        _deviceMock.Verify(d => d.SetAxis(2, It.Is<double>(v => Math.Abs(v - 1.0) < 0.0001)), Times.Once);
    }

    [Fact]
    public async Task Functor_YAxisIndexZero_DoesNotSetYAxis()
    {
        var config = new JsonObject { ["vjoyId"] = 1, ["xAxisIndex"] = 1, ["yAxisIndex"] = 0 };
        var functor = _descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(HatEvent(90));

        // yAxisIndex=0 → Y axis write skipped; X axis should get +1
        _deviceMock.Verify(d => d.SetAxis(1, It.Is<double>(v => Math.Abs(v - 1.0) < 0.0001)), Times.Once);
        _deviceMock.Verify(d => d.SetAxis(0, It.IsAny<double>()), Times.Never);
    }

    // ── Helper: access internal static ComputeAxisValues via reflection ───────

    private static (double x, double y) HatToAxisDescriptor_ComputeAxisValues(double hatValue)
    {
        // ComputeAxisValues is internal static on the private nested functor class.
        // Access via the public descriptor's reflected method.
        var functorType = typeof(HatToAxisDescriptor)
            .GetNestedType("HatToAxisFunctor", System.Reflection.BindingFlags.NonPublic)!;

        var method = functorType.GetMethod(
            "ComputeAxisValues",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var result = method.Invoke(null, [hatValue])!;
        var xField = result.GetType().GetField("Item1")!;
        var yField = result.GetType().GetField("Item2")!;
        return ((double)xField.GetValue(result)!, (double)yField.GetValue(result)!);
    }
}
