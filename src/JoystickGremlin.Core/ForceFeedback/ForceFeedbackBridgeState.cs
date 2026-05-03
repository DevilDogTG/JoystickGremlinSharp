// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Describes the current operational state of the <see cref="IForceFeedbackBridge"/>.
/// </summary>
public enum ForceFeedbackBridgeState
{
    /// <summary>The bridge has not been started (initial state).</summary>
    Disabled,

    /// <summary>The bridge is in the process of connecting the source and sink.</summary>
    Starting,

    /// <summary>The bridge is actively forwarding force feedback commands.</summary>
    Running,

    /// <summary>The bridge is running but no sink device is available.</summary>
    NoSink,

    /// <summary>The bridge is running but no source device is available.</summary>
    NoSource,

    /// <summary>The bridge is running in a degraded state (e.g. intermittent sink errors).</summary>
    Degraded,

    /// <summary>The bridge has been stopped cleanly.</summary>
    Stopped,

    /// <summary>The bridge encountered a fatal error and cannot recover.</summary>
    Error,
}
