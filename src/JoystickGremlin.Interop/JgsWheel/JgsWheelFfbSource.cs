// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.ForceFeedback;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.JgsWheel;

/// <summary>
/// <see cref="IForceFeedbackSource"/> implementation backed by the JGS Wheel driver.
/// </summary>
/// <remarks>
/// <para>
/// Because the JGS Wheel driver preserves the vJoy FFB ABI, once the driver is built
/// the implementation can wrap the same <c>FfbRegisterGenCB</c> call <c>VJoyFfbSource</c>
/// uses (loaded from <c>JgsWheelInterface.dll</c>). Until then, this source is a
/// graceful stub: <see cref="Start"/> is a no-op, <see cref="IsFfbCapable"/> reflects the
/// prerequisite probe, and <see cref="CommandReceived"/> never fires.
/// </para>
/// <para>
/// Wiring this source into the <see cref="ForceFeedbackBridge"/> (instead of
/// <see cref="VJoy.VJoyFfbSource"/>) lets the FFB pipeline switch backends without
/// changing the bridge or the sink.
/// </para>
/// </remarks>
public sealed class JgsWheelFfbSource : IForceFeedbackSource
{
    private readonly ILogger<JgsWheelFfbSource> _logger;
    private readonly JgsWheelPrerequisiteResult _prereq;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="JgsWheelFfbSource"/>.</summary>
    public JgsWheelFfbSource(ILogger<JgsWheelFfbSource> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _prereq = JgsWheelPrerequisiteChecker.Check();
    }

    /// <inheritdoc />
    public uint VJoyDeviceId => 1;

    /// <inheritdoc />
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    public bool IsFfbCapable => _prereq.IsOk;

    /// <inheritdoc />
    public event EventHandler<FfbCommand>? CommandReceived;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) return;

        if (!_prereq.IsOk)
        {
            _logger.LogWarning(
                "JgsWheelFfbSource.Start ignored — driver service '{Service}' not installed. " +
                "Build instructions: installer/wheel-driver/README.md.",
                JgsWheelPrerequisiteChecker.ServiceName);
            return;
        }

        // When the driver is available, wire the native FFB callback here using the
        // same approach as VJoyFfbSource (FfbRegisterGenCB from JgsWheelInterface.dll).
        _logger.LogInformation(
            "JgsWheelFfbSource.Start — driver detected at {ImagePath} but native FFB binding " +
            "is not yet implemented. Marking as running for bridge orchestration only.",
            _prereq.ImagePath);
        IsRunning = true;
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _logger.LogInformation("JgsWheelFfbSource stopped.");
    }

    /// <summary>For test use only — synthesises an FFB command on the source.</summary>
    internal void RaiseCommand(FfbCommand command) => CommandReceived?.Invoke(this, command);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        CommandReceived = null;
    }
}
