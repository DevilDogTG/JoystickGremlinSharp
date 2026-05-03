// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Orchestrates force feedback bridging between a virtual joystick source and a physical device sink.
/// </summary>
public interface IForceFeedbackBridge : IDisposable
{
    /// <summary>Gets the current operational state of the bridge.</summary>
    ForceFeedbackBridgeState State { get; }

    /// <summary>Gets the force feedback source, or <c>null</c> if none is configured.</summary>
    IForceFeedbackSource? Source { get; }

    /// <summary>Gets the force feedback sink, or <c>null</c> if none is configured.</summary>
    IForceFeedbackSink? Sink { get; }

    /// <summary>Raised whenever the bridge transitions to a new <see cref="ForceFeedbackBridgeState"/>.</summary>
    event EventHandler<ForceFeedbackBridgeState> StateChanged;

    /// <summary>Gets the total number of force feedback commands forwarded since the bridge started.</summary>
    long TotalCommandsForwarded { get; }

    /// <summary>Gets the time of the most recently forwarded command, or <c>null</c> if none have been forwarded.</summary>
    DateTimeOffset? LastCommandTime { get; }

    /// <summary>
    /// Starts the bridge: connects the sink, starts the source, and begins forwarding commands.
    /// </summary>
    /// <param name="cancellationToken">A token that may cancel the start operation.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the bridge: sends a stop-all command to the sink, stops the source, and disconnects the sink.
    /// </summary>
    /// <param name="cancellationToken">A token that may cancel the stop operation.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
