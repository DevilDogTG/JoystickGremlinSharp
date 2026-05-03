// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Orchestrates force feedback bridging between a <see cref="IForceFeedbackSource"/> (vJoy virtual device)
/// and a <see cref="IForceFeedbackSink"/> (physical wheel). Commands received from the source are
/// immediately forwarded to the sink.
/// </summary>
public sealed class ForceFeedbackBridge : IForceFeedbackBridge
{
    private readonly IForceFeedbackSource _source;
    private readonly IForceFeedbackSink _sink;
    private readonly ILogger<ForceFeedbackBridge> _logger;

    private volatile ForceFeedbackBridgeState _state = ForceFeedbackBridgeState.Disabled;
    private long _totalCommandsForwarded;
    private DateTimeOffset? _lastCommandTime;
    private bool _disposed;

    /// <summary>
    /// Initialises a new <see cref="ForceFeedbackBridge"/> with the specified source and sink.
    /// </summary>
    /// <param name="source">The force feedback source (vJoy FFB callback).</param>
    /// <param name="sink">The force feedback sink (physical wheel via DirectInput).</param>
    /// <param name="logger">Logger instance.</param>
    public ForceFeedbackBridge(
        IForceFeedbackSource source,
        IForceFeedbackSink sink,
        ILogger<ForceFeedbackBridge> logger)
    {
        _source = source;
        _sink = sink;
        _logger = logger;
    }

    /// <inheritdoc />
    public ForceFeedbackBridgeState State => _state;

    /// <inheritdoc />
    public IForceFeedbackSource Source => _source;

    /// <inheritdoc />
    public IForceFeedbackSink Sink => _sink;

    /// <inheritdoc />
    IForceFeedbackSource? IForceFeedbackBridge.Source => _source;

    /// <inheritdoc />
    IForceFeedbackSink? IForceFeedbackBridge.Sink => _sink;

    /// <inheritdoc />
    public event EventHandler<ForceFeedbackBridgeState>? StateChanged;

    /// <inheritdoc />
    public long TotalCommandsForwarded => Interlocked.Read(ref _totalCommandsForwarded);

    /// <inheritdoc />
    public DateTimeOffset? LastCommandTime => _lastCommandTime;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Starting force feedback bridge: vJoy device {DeviceId} → {SinkName}",
            _source.VJoyDeviceId, _sink.DisplayName);

        TransitionState(ForceFeedbackBridgeState.Starting);

        try
        {
            await _sink.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _source.CommandReceived += OnCommandReceived;
            _source.Start();
            TransitionState(ForceFeedbackBridgeState.Running);
            _logger.LogInformation("Force feedback bridge is running.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start force feedback bridge.");
            TransitionState(ForceFeedbackBridgeState.Error);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping force feedback bridge.");

        _source.CommandReceived -= OnCommandReceived;
        _source.Stop();

        if (_sink.IsConnected)
        {
            try
            {
                _sink.SendCommand(new DeviceControlCommand(FfbDeviceCommand.StopAll));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send StopAll command to sink during shutdown.");
            }

            _sink.Disconnect();
        }

        TransitionState(ForceFeedbackBridgeState.Stopped);
        _logger.LogInformation("Force feedback bridge stopped.");

        return Task.CompletedTask;
    }

    private void OnCommandReceived(object? sender, FfbCommand command)
    {
        if (_state == ForceFeedbackBridgeState.Stopped || _disposed)
        {
            return;
        }

        try
        {
            _sink.SendCommand(command);
            Interlocked.Increment(ref _totalCommandsForwarded);
            _lastCommandTime = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sink SendCommand failed; transitioning bridge to Degraded state.");
            TransitionState(ForceFeedbackBridgeState.Degraded);
        }
    }

    private void TransitionState(ForceFeedbackBridgeState newState)
    {
        _state = newState;
        StateChanged?.Invoke(this, newState);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _source.CommandReceived -= OnCommandReceived;

        if (_sink.IsConnected)
        {
            try
            {
                _sink.SendCommand(new DeviceControlCommand(FfbDeviceCommand.StopAll));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send StopAll command to sink during dispose.");
            }

            _sink.Disconnect();
        }

        _source.Dispose();
        _sink.Dispose();
    }
}
