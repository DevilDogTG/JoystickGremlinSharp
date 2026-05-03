// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.ForceFeedback;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JoystickGremlin.Core.Tests.ForceFeedback;

public sealed class ForceFeedbackBridgeTests
{
    private readonly Mock<IForceFeedbackSource> _sourceMock = new();
    private readonly Mock<IForceFeedbackSink> _sinkMock = new();
    private readonly Mock<ILogger<ForceFeedbackBridge>> _loggerMock = new();

    private ForceFeedbackBridge CreateSut()
    {
        _sinkMock.Setup(s => s.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _sinkMock.Setup(s => s.IsConnected).Returns(true);
        _sinkMock.Setup(s => s.DisplayName).Returns("Mock Wheel");

        _sourceMock.Setup(s => s.VJoyDeviceId).Returns(1u);

        return new ForceFeedbackBridge(_sourceMock.Object, _sinkMock.Object, _loggerMock.Object);
    }

    // ── State transitions ─────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsDisabled()
    {
        using var sut = CreateSut();

        sut.State.Should().Be(ForceFeedbackBridgeState.Disabled);
    }

    [Fact]
    public async Task StartAsync_TransitionsToRunning()
    {
        using var sut = CreateSut();
        var stateHistory = new List<ForceFeedbackBridgeState>();
        sut.StateChanged += (_, s) => stateHistory.Add(s);

        await sut.StartAsync();

        sut.State.Should().Be(ForceFeedbackBridgeState.Running);
        stateHistory.Should().ContainInOrder(
            ForceFeedbackBridgeState.Starting,
            ForceFeedbackBridgeState.Running);
    }

    [Fact]
    public async Task StopAsync_TransitionsToStopped()
    {
        using var sut = CreateSut();
        await sut.StartAsync();

        await sut.StopAsync();

        sut.State.Should().Be(ForceFeedbackBridgeState.Stopped);
    }

    // ── Command forwarding ────────────────────────────────────────────────────

    [Fact]
    public async Task CommandReceived_ForwardsToSink()
    {
        using var sut = CreateSut();
        await sut.StartAsync();

        var command = new SetConstantForceCommand(1, 5000);
        _sourceMock.Raise(s => s.CommandReceived += null, _sourceMock.Object, (FfbCommand)command);

        _sinkMock.Verify(s => s.SendCommand(command), Times.Once);
    }

    [Fact]
    public async Task CommandReceived_IncrementsCounter()
    {
        using var sut = CreateSut();
        await sut.StartAsync();

        var command = new SetConstantForceCommand(1, 5000);
        _sourceMock.Raise(s => s.CommandReceived += null, _sourceMock.Object, (FfbCommand)command);
        _sourceMock.Raise(s => s.CommandReceived += null, _sourceMock.Object, (FfbCommand)command);
        _sourceMock.Raise(s => s.CommandReceived += null, _sourceMock.Object, (FfbCommand)command);

        sut.TotalCommandsForwarded.Should().Be(3);
    }

    [Fact]
    public async Task CommandReceived_UpdatesLastCommandTime()
    {
        using var sut = CreateSut();
        await sut.StartAsync();
        sut.LastCommandTime.Should().BeNull();

        var before = DateTimeOffset.UtcNow;
        var command = new SetConstantForceCommand(1, 5000);
        _sourceMock.Raise(s => s.CommandReceived += null, _sourceMock.Object, (FfbCommand)command);

        sut.LastCommandTime.Should().NotBeNull();
        sut.LastCommandTime!.Value.Should().BeOnOrAfter(before);
    }

    // ── StopAsync sends StopAll before Disconnect ─────────────────────────────

    [Fact]
    public async Task StopAsync_SendsStopAllThenDisconnects()
    {
        using var sut = CreateSut();
        await sut.StartAsync();

        var callOrder = new List<string>();
        _sinkMock.Setup(s => s.SendCommand(It.IsAny<FfbCommand>()))
            .Callback<FfbCommand>(_ => callOrder.Add("SendCommand"));
        _sinkMock.Setup(s => s.Disconnect())
            .Callback(() => callOrder.Add("Disconnect"));

        await sut.StopAsync();

        callOrder.Should().ContainInOrder("SendCommand", "Disconnect");
        _sinkMock.Verify(s => s.SendCommand(
            It.Is<DeviceControlCommand>(c => c.Control == FfbDeviceCommand.StopAll)), Times.Once);
        _sinkMock.Verify(s => s.Disconnect(), Times.Once);
    }

    // ── Sink exception transitions to Degraded ────────────────────────────────

    [Fact]
    public async Task SinkSendCommandException_TransitionsToDegraded()
    {
        using var sut = CreateSut();
        await sut.StartAsync();

        _sinkMock.Setup(s => s.SendCommand(It.IsAny<FfbCommand>()))
            .Throws(new InvalidOperationException("Simulated sink failure"));

        var stateHistory = new List<ForceFeedbackBridgeState>();
        sut.StateChanged += (_, s) => stateHistory.Add(s);

        var command = new SetConstantForceCommand(1, 5000);
        _sourceMock.Raise(s => s.CommandReceived += null, _sourceMock.Object, (FfbCommand)command);

        sut.State.Should().Be(ForceFeedbackBridgeState.Degraded);
        stateHistory.Should().Contain(ForceFeedbackBridgeState.Degraded);
    }

    // ── Source and Sink properties ─────────────────────────────────────────────

    [Fact]
    public void Source_ReturnsConfiguredSource()
    {
        using var sut = CreateSut();

        ((IForceFeedbackBridge)sut).Source.Should().Be(_sourceMock.Object);
    }

    [Fact]
    public void Sink_ReturnsConfiguredSink()
    {
        using var sut = CreateSut();

        ((IForceFeedbackBridge)sut).Sink.Should().Be(_sinkMock.Object);
    }
}
