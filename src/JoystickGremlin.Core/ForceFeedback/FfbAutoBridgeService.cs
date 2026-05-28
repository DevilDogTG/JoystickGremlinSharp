// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Ties the force feedback bridge lifecycle to <see cref="IEventPipeline.Started"/> /
/// <see cref="IEventPipeline.Stopped"/>, so the bridge starts and stops along with the pipeline
/// regardless of whether the pipeline was started manually or by the auto-load process monitor.
/// Mirrors the lifecycle hook in <see cref="HidHide.HidHideManager"/>.
/// </summary>
public sealed class FfbAutoBridgeService : IDisposable
{
    private readonly IForceFeedbackBridge _bridge;
    private readonly ISettingsService _settings;
    private readonly IEventPipeline _pipeline;
    private readonly ILogger<FfbAutoBridgeService> _logger;
    private bool _disposed;

    /// <summary>Initializes a new instance of <see cref="FfbAutoBridgeService"/>.</summary>
    public FfbAutoBridgeService(
        IForceFeedbackBridge bridge,
        ISettingsService settings,
        IEventPipeline pipeline,
        ILogger<FfbAutoBridgeService> logger)
    {
        _bridge   = bridge;
        _settings = settings;
        _pipeline = pipeline;
        _logger   = logger;

        _pipeline.Started += OnPipelineStarted;
        _pipeline.Stopped += OnPipelineStopped;
    }

    /// <summary>
    /// Reacts to pipeline start by initiating an asynchronous FFB bridge start if the user has
    /// enabled the bridge. Fire-and-forget so a slow/failing bridge does not stall the pipeline.
    /// </summary>
    private void OnPipelineStarted(object? sender, EventArgs e)
    {
        if (!_settings.Settings.EnableFfbBridge)
        {
            return;
        }

        _ = StartFfbBridgeAsync();
    }

    /// <summary>
    /// Reacts to pipeline stop by tearing down the FFB bridge unless it is already in a
    /// non-running state (Disabled/Stopped), which would make a StopAsync call a no-op.
    /// </summary>
    private void OnPipelineStopped(object? sender, EventArgs e)
    {
        if (_bridge.State is ForceFeedbackBridgeState.Disabled or ForceFeedbackBridgeState.Stopped)
        {
            return;
        }

        _ = StopFfbBridgeAsync();
    }

    /// <summary>Starts the FFB bridge, logging and swallowing any startup exception.</summary>
    private async Task StartFfbBridgeAsync()
    {
        try
        {
            _logger.LogInformation(
                "Starting FFB bridge in response to pipeline start (vJoy device {DeviceId}).",
                _settings.Settings.FfbVJoyDeviceId);
            await _bridge.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFB bridge failed to start; continuing without FFB.");
        }
    }

    /// <summary>Stops the FFB bridge, logging and swallowing any shutdown exception.</summary>
    private async Task StopFfbBridgeAsync()
    {
        try
        {
            await _bridge.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFB bridge failed to stop.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _pipeline.Started -= OnPipelineStarted;
        _pipeline.Stopped -= OnPipelineStopped;
        _disposed = true;
    }
}
