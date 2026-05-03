// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using FluentAssertions;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class ButtonsToAxesFunctorTests
{
    private readonly Mock<IVirtualDeviceManager> _virtualDeviceManagerMock = new();
    private readonly Mock<IVirtualDevice> _virtualDeviceMock = new();
    private readonly Mock<ILogger<ButtonsToAxesDescriptor>> _loggerMock = new();
    private readonly ButtonsToAxesDescriptor _descriptor;

    public ButtonsToAxesFunctorTests()
    {
        _virtualDeviceManagerMock
            .Setup(m => m.AcquireDevice(It.IsAny<uint>()))
            .Returns(_virtualDeviceMock.Object);

        _descriptor = new ButtonsToAxesDescriptor(_virtualDeviceManagerMock.Object, _loggerMock.Object);
    }

    private JsonObject CreateConfig(uint vjoyId = 1, int xAxisIndex = 1, int yAxisIndex = 2,
        int upButtonId = 1, int downButtonId = 2, int leftButtonId = 3, int rightButtonId = 4)
    {
        var config = new JsonObject
        {
            ["vjoyId"] = (int)vjoyId,
            ["xAxisIndex"] = xAxisIndex,
            ["yAxisIndex"] = yAxisIndex,
            ["upButtonId"] = upButtonId,
            ["downButtonId"] = downButtonId,
            ["leftButtonId"] = leftButtonId,
            ["rightButtonId"] = rightButtonId
        };
        return config;
    }

    private InputEvent CreateButtonEvent(int buttonId, double value, string mode = "default")
    {
        return new InputEvent(
            InputType.JoystickButton,
            Guid.NewGuid(),
            buttonId,
            value,
            mode);
    }

    [Fact]
    public async Task ExecuteAsync_NoButtonPressed_SetsBothAxesToZero()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(1, 0.0); // Up button pressed but released

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once);
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpButtonPressed_SetsYAxisTo1_0()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(1, 1.0); // Up button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(2, 1.0), Times.Once); // Y axis
        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
    }

    [Fact]
    public async Task ExecuteAsync_DownButtonPressed_SetsYAxisToNegative1_0()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(2, 1.0); // Down button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(2, -1.0), Times.Once); // Y axis
        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
    }

    [Fact]
    public async Task ExecuteAsync_LeftButtonPressed_SetsXAxisToNegative1_0()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(3, 1.0); // Left button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(1, -1.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_RightButtonPressed_SetsXAxisTo1_0()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(4, 1.0); // Right button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 1.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_UpAndRightPressed_SetsXTo1_0AndYTo1_0()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press Up
        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0));
        _virtualDeviceMock.Reset();

        // Press Right
        await functor.ExecuteAsync(CreateButtonEvent(4, 1.0));

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 1.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 1.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_UpAndDownPressed_SetsYAxisToZero()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press Up
        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0));
        _virtualDeviceMock.Reset();

        // Press Down (Up + Down = Y = 0)
        await functor.ExecuteAsync(CreateButtonEvent(2, 1.0));

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_LeftAndRightPressed_SetsXAxisToZero()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press Left
        await functor.ExecuteAsync(CreateButtonEvent(3, 1.0));
        _virtualDeviceMock.Reset();

        // Press Right (Left + Right = X = 0)
        await functor.ExecuteAsync(CreateButtonEvent(4, 1.0));

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_ButtonPressedThenReleased_StateUpdates()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press Up
        var pressEvent = CreateButtonEvent(1, 1.0);
        await functor.ExecuteAsync(pressEvent);
        _virtualDeviceMock.Reset();

        // Release Up
        var releaseEvent = CreateButtonEvent(1, 0.0);
        await functor.ExecuteAsync(releaseEvent);

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_VJoyOwnershipLost_ReacquiresAndRetries()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        var setAxisCallCount = 0;
        _virtualDeviceMock
            .Setup(v => v.SetAxis(It.IsAny<int>(), It.IsAny<double>()))
            .Callback(() =>
            {
                setAxisCallCount++;
                if (setAxisCallCount == 1)
                    throw new VJoyException("Device ownership lost");
            });

        _virtualDeviceManagerMock
            .Setup(m => m.AcquireDevice(1))
            .Returns(_virtualDeviceMock.Object);

        _virtualDeviceManagerMock
            .Setup(m => m.ReleaseDevice(1));

        var @event = CreateButtonEvent(1, 1.0);
        await functor.ExecuteAsync(@event);

        _virtualDeviceManagerMock.Verify(m => m.ReleaseDevice(1), Times.Once);
        _virtualDeviceManagerMock.Verify(m => m.AcquireDevice(1), Times.AtLeast(2));
        setAxisCallCount.Should().Be(3); // First call threw on X, retry both X and Y
    }

    [Fact]
    public async Task ExecuteAsync_UnknownButtonId_IgnoresEvent()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(99, 1.0); // Unknown button

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(It.IsAny<int>(), It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CustomVJoyAndAxisIndices_UsesCorrectIndices()
    {
        var config = CreateConfig(vjoyId: 2, xAxisIndex: 5, yAxisIndex: 6,
            upButtonId: 10, downButtonId: 11, leftButtonId: 12, rightButtonId: 13);
        var functor = _descriptor.CreateFunctor(config);
        
        _virtualDeviceManagerMock
            .Setup(m => m.AcquireDevice(2))
            .Returns(_virtualDeviceMock.Object);
        
        var @event = CreateButtonEvent(10, 1.0); // Up button

        await functor.ExecuteAsync(@event);

        _virtualDeviceManagerMock.Verify(m => m.AcquireDevice(2), Times.AtLeast(1));
        _virtualDeviceMock.Verify(v => v.SetAxis(5, 0.0), Times.Once); // X axis (no left/right)
        _virtualDeviceMock.Verify(v => v.SetAxis(6, 1.0), Times.Once); // Y axis (up pressed)
    }

    [Fact]
    public async Task CreateFunctor_WithoutConfiguration_UsesDefaults()
    {
        var functor = _descriptor.CreateFunctor(null);
        var @event = CreateButtonEvent(1, 1.0); // Default up button

        // Should throw or ignore since defaults are -1
        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(It.IsAny<int>(), It.IsAny<double>()), Times.Never);
    }

    [Theory]
    [InlineData(0.0)] // Button released
    [InlineData(0.3)] // Below threshold
    public async Task ExecuteAsync_ValueBelowThreshold_TreatsAsReleased(double value)
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // First press Up
        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0));
        _virtualDeviceMock.Reset();

        // Then release with value < 0.5
        await functor.ExecuteAsync(CreateButtonEvent(1, value));

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once); // Y axis
    }

    [Theory]
    [InlineData(0.5)] // At threshold
    [InlineData(1.0)] // Full press
    public async Task ExecuteAsync_ValueAtOrAboveThreshold_TreatsAsPressed(double value)
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(1, value);

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 1.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_ComplexButtonSequence_MaintainsIndependentAxisState()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press Up (Y = 1, X = 0)
        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0));
        _virtualDeviceMock.Reset();

        // Press Right (Y = 1, X = 1)
        await functor.ExecuteAsync(CreateButtonEvent(4, 1.0));
        _virtualDeviceMock.Verify(v => v.SetAxis(1, 1.0), Times.Once);
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 1.0), Times.Once);
        _virtualDeviceMock.Reset();

        // Release Up (Y = 0, X = 1)
        await functor.ExecuteAsync(CreateButtonEvent(1, 0.0));
        _virtualDeviceMock.Verify(v => v.SetAxis(1, 1.0), Times.Once);
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once);
        _virtualDeviceMock.Reset();

        // Press Left (Y = 0, X = 0 because Left + Right = 0)
        await functor.ExecuteAsync(CreateButtonEvent(3, 1.0));
        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once);
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AllFourDirectionsPressed_AllAxesToZero()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press all four
        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0)); // Up
        _virtualDeviceMock.Reset();
        await functor.ExecuteAsync(CreateButtonEvent(2, 1.0)); // Down
        _virtualDeviceMock.Reset();
        await functor.ExecuteAsync(CreateButtonEvent(3, 1.0)); // Left
        _virtualDeviceMock.Reset();
        await functor.ExecuteAsync(CreateButtonEvent(4, 1.0)); // Right

        _virtualDeviceMock.Verify(v => v.SetAxis(1, 0.0), Times.Once); // X axis
        _virtualDeviceMock.Verify(v => v.SetAxis(2, 0.0), Times.Once); // Y axis
    }

    [Fact]
    public async Task ExecuteAsync_BothAxesUpdatedAtomically()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        var updateSequence = new List<(int, double)>();
        _virtualDeviceMock
            .Setup(v => v.SetAxis(It.IsAny<int>(), It.IsAny<double>()))
            .Callback<int, double>((axis, value) => updateSequence.Add((axis, value)));

        // Press Up and Right simultaneously (shouldn't happen in practice, but test atomicity)
        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0)); // Up
        updateSequence.Clear();
        await functor.ExecuteAsync(CreateButtonEvent(4, 1.0)); // Right

        // Both axes should be set in one call sequence
        updateSequence.Should().HaveCount(2);
        updateSequence.Should().Contain((1, 1.0)); // X
        updateSequence.Should().Contain((2, 1.0)); // Y
    }
}
