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

public sealed class ButtonsToHatFunctorTests
{
    private readonly Mock<IVirtualDeviceManager> _virtualDeviceManagerMock = new();
    private readonly Mock<IVirtualDevice> _virtualDeviceMock = new();
    private readonly Mock<ILogger<ButtonsToHatDescriptor>> _loggerMock = new();
    private readonly ButtonsToHatDescriptor _descriptor;

    public ButtonsToHatFunctorTests()
    {
        _virtualDeviceManagerMock
            .Setup(m => m.GetDevice(It.IsAny<uint>()))
            .Returns(_virtualDeviceMock.Object);

        _descriptor = new ButtonsToHatDescriptor(_virtualDeviceManagerMock.Object, _loggerMock.Object);
    }

    private JsonObject CreateConfig(uint vjoyId = 1, int hatIndex = 1,
        int upButtonId = 1, int downButtonId = 2, int leftButtonId = 3, int rightButtonId = 4)
    {
        var config = new JsonObject
        {
            ["vjoyId"] = (int)vjoyId,
            ["hatIndex"] = hatIndex,
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
    public async Task ExecuteAsync_NoButtonPressed_SetsHatToCenter()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(1, 0.0); // Up button pressed but with 0 value = released

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetHat(1, -1), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpButtonPressed_SetsHatTo0Degrees()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(1, 1.0); // Up button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetHat(1, 0), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DownButtonPressed_SetsHatTo180Degrees()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(2, 1.0); // Down button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetHat(1, 18000), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LeftButtonPressed_SetsHatTo270Degrees()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(3, 1.0); // Left button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetHat(1, 27000), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RightButtonPressed_SetsHatTo90Degrees()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(4, 1.0); // Right button pressed

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetHat(1, 9000), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpAndRightPressed_SetsHatTo45Degrees()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        var upEvent = CreateButtonEvent(1, 1.0);
        await functor.ExecuteAsync(upEvent);
        _virtualDeviceMock.Reset();

        var rightEvent = CreateButtonEvent(4, 1.0);
        await functor.ExecuteAsync(rightEvent);

        _virtualDeviceMock.Verify(v => v.SetHat(1, 4500), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpAndDownPressed_SetsHatToCenter()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        var upEvent = CreateButtonEvent(1, 1.0);
        await functor.ExecuteAsync(upEvent);
        _virtualDeviceMock.Reset();

        var downEvent = CreateButtonEvent(2, 1.0);
        await functor.ExecuteAsync(downEvent);

        // Up + Down = center (-1)
        _virtualDeviceMock.Verify(v => v.SetHat(1, -1), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ButtonPressedThenReleased_StateUpdates()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press Up
        var pressEvent = CreateButtonEvent(1, 1.0);
        await functor.ExecuteAsync(pressEvent);
        _virtualDeviceMock.Verify(v => v.SetHat(1, 0), Times.Once);
        _virtualDeviceMock.Reset();

        // Release Up
        var releaseEvent = CreateButtonEvent(1, 0.0);
        await functor.ExecuteAsync(releaseEvent);
        _virtualDeviceMock.Verify(v => v.SetHat(1, -1), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpLeftDown_StateMaintained()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        // Press Up
        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0));
        _virtualDeviceMock.Reset();

        // Press Left (diagonally opposite on X, same on Y)
        await functor.ExecuteAsync(CreateButtonEvent(3, 1.0));
        _virtualDeviceMock.Verify(v => v.SetHat(1, 31500), Times.Once); // Up-Left
        _virtualDeviceMock.Reset();

        // Press Down (Up + Down = center)
        await functor.ExecuteAsync(CreateButtonEvent(2, 1.0));
        _virtualDeviceMock.Verify(v => v.SetHat(1, -1), Times.Once); // Centered
    }

    [Fact]
    public async Task ExecuteAsync_VJoyOwnershipLost_ReacquiresAndRetries()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        var callCount = 0;
        _virtualDeviceMock
            .Setup(v => v.SetHat(It.IsAny<int>(), It.IsAny<int>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new VJoyException("Device ownership lost");
            });

        _virtualDeviceManagerMock
            .Setup(m => m.GetDevice(1))
            .Throws(new VJoyException("Device not acquired"));

        _virtualDeviceManagerMock
            .Setup(m => m.AcquireDevice(1))
            .Returns(_virtualDeviceMock.Object);

        _virtualDeviceManagerMock
            .Setup(m => m.ReleaseDevice(1));

        var @event = CreateButtonEvent(1, 1.0);
        await functor.ExecuteAsync(@event);

        _virtualDeviceManagerMock.Verify(m => m.ReleaseDevice(1), Times.Once);
        _virtualDeviceManagerMock.Verify(m => m.AcquireDevice(1), Times.AtLeast(2));
        callCount.Should().Be(2); // First call threw, second call succeeded
    }

    [Fact]
    public async Task ExecuteAsync_UnknownButtonId_IgnoresEvent()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);
        var @event = CreateButtonEvent(99, 1.0); // Unknown button

        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetHat(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CustomVJoyAndHatIndices_UsesCorrectIndices()
    {
        var config = CreateConfig(vjoyId: 2, hatIndex: 3, upButtonId: 10, downButtonId: 11, leftButtonId: 12, rightButtonId: 13);
        var functor = _descriptor.CreateFunctor(config);
        
        _virtualDeviceManagerMock
            .Setup(m => m.GetDevice(2))
            .Returns(_virtualDeviceMock.Object);
        
        var @event = CreateButtonEvent(10, 1.0);

        await functor.ExecuteAsync(@event);

        _virtualDeviceManagerMock.Verify(m => m.GetDevice(2), Times.Once);
        _virtualDeviceMock.Verify(v => v.SetHat(3, 0), Times.Once);
    }

    [Fact]
    public async Task CreateFunctor_WithoutConfiguration_UsesDefaults()
    {
        var functor = _descriptor.CreateFunctor(null);
        var @event = CreateButtonEvent(1, 1.0); // Default up button

        // Should throw or ignore since defaults are -1
        await functor.ExecuteAsync(@event);

        _virtualDeviceMock.Verify(v => v.SetHat(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
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

        _virtualDeviceMock.Verify(v => v.SetHat(1, -1), Times.Once);
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

        _virtualDeviceMock.Verify(v => v.SetHat(1, 0), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AllFourDirectionsCombined_SetsHatToCenter()
    {
        var config = CreateConfig();
        var functor = _descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(CreateButtonEvent(1, 1.0)); // Up
        _virtualDeviceMock.Reset();
        await functor.ExecuteAsync(CreateButtonEvent(2, 1.0)); // Down
        _virtualDeviceMock.Reset();
        await functor.ExecuteAsync(CreateButtonEvent(3, 1.0)); // Left
        _virtualDeviceMock.Reset();
        await functor.ExecuteAsync(CreateButtonEvent(4, 1.0)); // Right

        _virtualDeviceMock.Verify(v => v.SetHat(1, -1), Times.Once);
    }
}
