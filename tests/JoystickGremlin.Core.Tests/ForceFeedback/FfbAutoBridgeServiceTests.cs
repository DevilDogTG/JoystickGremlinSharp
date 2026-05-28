// SPDX-License-Identifier: GPL-3.0-only

using System.Threading;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ForceFeedback;
using JoystickGremlin.Core.Pipeline;
using Microsoft.Extensions.Logging;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Tests.ForceFeedback;

/// <summary>
/// Verifies <see cref="FfbAutoBridgeService"/>'s pipeline-event-driven lifecycle: it should
/// honour <see cref="AppSettings.EnableFfbBridge"/> when the pipeline starts, skip stop calls
/// when the bridge is already in a non-running state, and never propagate bridge exceptions.
/// </summary>
public sealed class FfbAutoBridgeServiceTests
{
    /// <summary>Minimal <see cref="IEventPipeline"/> that lets tests fire Started/Stopped manually.</summary>
    private sealed class FakePipeline : IEventPipeline
    {
        /// <inheritdoc/>
        public bool IsRunning { get; private set; }

        /// <inheritdoc/>
        public event EventHandler? Started;

        /// <inheritdoc/>
        public event EventHandler? Stopped;

        /// <inheritdoc/>
        public void Start(ProfileModel profile)
        {
            IsRunning = true;
            Started?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            IsRunning = false;
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void Dispose() { /* no-op */ }
    }

    // ── Common setup ──────────────────────────────────────────────────────────

    private readonly Mock<IForceFeedbackBridge> _bridgeMock = new();
    private readonly Mock<ISettingsService> _settingsMock = new();
    private readonly Mock<ILogger<FfbAutoBridgeService>> _loggerMock = new();
    private readonly FakePipeline _pipeline = new();
    private AppSettings _appSettings = new()
    {
        EnableFfbBridge = true,
        FfbVJoyDeviceId = 1,
    };

    private FfbAutoBridgeService CreateSut()
    {
        _settingsMock.Setup(s => s.Settings).Returns(() => _appSettings);
        // Default: bridge is in a running-ish state so Stop calls aren't pre-empted.
        _bridgeMock.SetupGet(b => b.State).Returns(ForceFeedbackBridgeState.Running);
        _bridgeMock
            .Setup(b => b.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _bridgeMock
            .Setup(b => b.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new FfbAutoBridgeService(
            _bridgeMock.Object,
            _settingsMock.Object,
            _pipeline,
            _loggerMock.Object);
    }

    private static ProfileModel MakeProfile() => new() { Name = "test" };

    // ── Started: enable gating ───────────────────────────────────────────────

    [Fact]
    public void PipelineStarted_EnableFfbBridgeFalse_DoesNotStartBridge()
    {
        _appSettings.EnableFfbBridge = false;
        using var sut = CreateSut();

        _pipeline.Start(MakeProfile());

        _bridgeMock.Verify(
            b => b.StartAsync(It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public void PipelineStarted_EnableFfbBridgeTrue_StartsBridgeOnce()
    {
        _appSettings.EnableFfbBridge = true;
        using var sut = CreateSut();

        _pipeline.Start(MakeProfile());

        _bridgeMock.Verify(
            b => b.StartAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }

    // ── Stopped: skip when bridge already in non-running state ───────────────

    [Theory]
    [InlineData(ForceFeedbackBridgeState.Disabled)]
    [InlineData(ForceFeedbackBridgeState.Stopped)]
    public void PipelineStopped_BridgeNotRunning_DoesNotCallStop(ForceFeedbackBridgeState state)
    {
        using var sut = CreateSut();
        _bridgeMock.SetupGet(b => b.State).Returns(state);

        _pipeline.Stop();

        _bridgeMock.Verify(
            b => b.StopAsync(It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public void PipelineStopped_BridgeRunning_StopsBridge()
    {
        using var sut = CreateSut();
        _bridgeMock.SetupGet(b => b.State).Returns(ForceFeedbackBridgeState.Running);

        _pipeline.Stop();

        _bridgeMock.Verify(
            b => b.StopAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }

    // ── Exception swallowing ─────────────────────────────────────────────────

    [Fact]
    public void PipelineStarted_BridgeStartThrows_DoesNotPropagate()
    {
        _appSettings.EnableFfbBridge = true;
        _bridgeMock
            .Setup(b => b.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated"));
        using var sut = CreateSut();

        var act = () => _pipeline.Start(MakeProfile());

        act.Should().NotThrow();
        _bridgeMock.Verify(
            b => b.StartAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public void PipelineStopped_BridgeStopThrows_DoesNotPropagate()
    {
        _bridgeMock.SetupGet(b => b.State).Returns(ForceFeedbackBridgeState.Running);
        _bridgeMock
            .Setup(b => b.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated"));
        using var sut = CreateSut();

        var act = () => _pipeline.Stop();

        act.Should().NotThrow();
        _bridgeMock.Verify(
            b => b.StopAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_UnsubscribesFromPipeline_NoFurtherStartCalls()
    {
        _appSettings.EnableFfbBridge = true;
        var sut = CreateSut();
        sut.Dispose();

        _pipeline.Start(MakeProfile());

        _bridgeMock.Verify(
            b => b.StartAsync(It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var sut = CreateSut();
        sut.Dispose();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }
}
