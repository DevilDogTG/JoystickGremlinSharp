// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// No-op implementation of <see cref="IForceFeedbackBridge"/> used when the FFB bridge is
/// not configured or the interop layer has not registered a real implementation.
/// </summary>
public sealed class NullForceFeedbackBridge : IForceFeedbackBridge
{
    /// <inheritdoc />
    public ForceFeedbackBridgeState State => ForceFeedbackBridgeState.Disabled;

    /// <inheritdoc />
    public IForceFeedbackSource? Source => null;

    /// <inheritdoc />
    public IForceFeedbackSink? Sink => null;

    /// <inheritdoc />
    public long TotalCommandsForwarded => 0;

    /// <inheritdoc />
    public DateTimeOffset? LastCommandTime => null;

    /// <inheritdoc />
#pragma warning disable CS0067 // Event is never used — intentional for no-op implementation
    public event EventHandler<ForceFeedbackBridgeState>? StateChanged;
#pragma warning restore CS0067

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() { }
}
