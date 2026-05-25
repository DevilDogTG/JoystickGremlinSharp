// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Orchestrates HidHide device hiding tied to the event-pipeline lifecycle.
/// Hides devices when the pipeline starts; unhides them when the pipeline stops or the app exits.
/// </summary>
public sealed class HidHideManager : IHidHideManager
{
    private readonly IHidHideController _controller;
    private readonly ISettingsService _settingsService;
    private readonly IEventPipeline _pipeline;
    private readonly ILogger<HidHideManager> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _ownExePath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="HidHideManager"/> and performs crash-recovery revert
    /// to clear any orphaned hidden devices from a previous session.
    /// </summary>
    public HidHideManager(
        IHidHideController controller,
        ISettingsService settingsService,
        IEventPipeline pipeline,
        ILogger<HidHideManager> logger)
    {
        _controller = controller;
        _settingsService = settingsService;
        _pipeline = pipeline;
        _logger = logger;

        _ownExePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

        _pipeline.Started += OnPipelineStarted;
        _pipeline.Stopped += OnPipelineStopped;

        UpdateStatus();
    }

    /// <inheritdoc/>
    public HidHideStatus Status { get; private set; } = HidHideStatus.Disabled;

    /// <inheritdoc/>
    public string? LastError { get; private set; }

    /// <inheritdoc/>
    public bool IsApplied { get; private set; }

    /// <inheritdoc/>
    public event EventHandler? StatusChanged;

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Crash-recovery: revert any stale hidden devices from a previous session.
        // We do this regardless of the EnableHidHide setting because the previous session
        // might have had it enabled when it crashed.
        if (!_controller.IsInstalled)
        {
            UpdateStatus();
            return;
        }

        try
        {
            _controller.Refresh();
            var settings = _settingsService.Settings;

            // Crash-recovery: remove device instance IDs we may have left blocked after a crash.
            foreach (var instanceId in settings.HiddenDeviceInstanceIds)
            {
                if (_controller.BlockedInstanceIds.Contains(instanceId, StringComparer.OrdinalIgnoreCase))
                {
                    _controller.RemoveBlockedInstance(instanceId);
                    _logger.LogInformation("HidHide startup recovery: unblocked device '{InstanceId}'", instanceId);
                }
            }

            _logger.LogInformation("HidHide startup recovery complete");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HidHide startup recovery failed — driver may be unavailable");
        }

        // Always whitelist own executable so the app can see devices that HidHide may be hiding.
        // This is independent of EnableHidHide — the whitelist entry is needed whenever HidHide
        // is installed, regardless of whether this app manages hiding itself.
        await EnsureWhitelistedAsync(cancellationToken).ConfigureAwait(false);

        UpdateStatus();
    }

    /// <inheritdoc/>
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Settings;
        if (!settings.EnableHidHide)
        {
            _logger.LogDebug("HidHide integration is disabled — skipping Apply");
            return;
        }

        if (!_controller.IsInstalled)
        {
            SetStatus(HidHideStatus.NotInstalled);
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsApplied)
                return;

            _controller.Refresh();

            // Whitelist own executable — read first (no admin); write only if absent.
            if (!string.IsNullOrEmpty(_ownExePath) &&
                !_controller.ApplicationPaths.Contains(_ownExePath, StringComparer.OrdinalIgnoreCase))
            {
                _controller.AddApplicationPath(_ownExePath);
                _logger.LogInformation("HidHide: whitelisted own executable '{Path}'", _ownExePath);
            }

            // Block configured device instance IDs — read first (no admin); write only if not already blocked.
            foreach (var instanceId in settings.HiddenDeviceInstanceIds)
            {
                if (!_controller.BlockedInstanceIds.Contains(instanceId, StringComparer.OrdinalIgnoreCase))
                {
                    _controller.AddBlockedInstance(instanceId);
                    _logger.LogInformation("HidHide: blocking device '{InstanceId}'", instanceId);
                }
            }

            // Gate on — read first; write only if not already active.
            if (!_controller.IsActive)
                _controller.IsActive = true;
            IsApplied = true;
            SetStatus(HidHideStatus.Active);
            _logger.LogInformation("HidHide: hiding applied ({Count} device(s))", settings.HiddenDeviceInstanceIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HidHide: Apply failed");
            LastError = ex.Message;
            SetStatus(HidHideStatus.Error);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RevertAsync(CancellationToken cancellationToken = default)
    {
        if (!_controller.IsInstalled)
        {
            IsApplied = false;
            UpdateStatus();
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsApplied)
                return;

            _controller.Refresh();

            // Remove only the instance IDs we are responsible for — read first (no admin);
            // write only if the entry is actually present (avoids unnecessary CLI calls).
            var settings = _settingsService.Settings;
            foreach (var instanceId in settings.HiddenDeviceInstanceIds)
            {
                if (_controller.BlockedInstanceIds.Contains(instanceId, StringComparer.OrdinalIgnoreCase))
                {
                    _controller.RemoveBlockedInstance(instanceId);
                    _logger.LogInformation("HidHide: unblocked device '{InstanceId}'", instanceId);
                }
            }

            // Keep own exe in the whitelist — we re-add it at startup so games can find
            // the physical devices through us. The whitelist is cleaned up on Dispose (app exit).

            // Re-read the block list after our removes to avoid disabling the gate when
            // another application (e.g. HidHide config client) has its own entries present.
            _controller.Refresh();
            if (_controller.BlockedInstanceIds.Count == 0)
                _controller.IsActive = false;

            IsApplied = false;
            UpdateStatus();
            _logger.LogInformation("HidHide: hiding reverted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HidHide: Revert failed");
            LastError = ex.Message;
            SetStatus(HidHideStatus.Error);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_controller.IsInstalled)
            _controller.Refresh();

        UpdateStatus();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _pipeline.Started -= OnPipelineStarted;
        _pipeline.Stopped -= OnPipelineStopped;

        // Synchronous revert on dispose — best-effort with short timeout to avoid
        // blocking the UI thread indefinitely if the semaphore is held.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            RevertAsync(cts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("HidHide: RevertAsync timed out during Dispose");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HidHide: RevertAsync failed during Dispose");
        }

        // Remove own exe from whitelist on clean exit — read first (no admin); write only if present.
        if (!string.IsNullOrEmpty(_ownExePath) && _controller.IsInstalled)
        {
            try
            {
                if (_controller.ApplicationPaths.Contains(_ownExePath, StringComparer.OrdinalIgnoreCase))
                {
                    _controller.RemoveApplicationPath(_ownExePath);
                    _logger.LogDebug("HidHide: removed own exe from whitelist on exit");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HidHide: failed to remove own exe from whitelist on exit");
            }
        }

        _lock.Dispose();
        _disposed = true;
    }

    // ── Whitelist helper ─────────────────────────────────────────────────────

    private async Task EnsureWhitelistedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_ownExePath) || !_controller.IsInstalled)
            return;

        await Task.CompletedTask.ConfigureAwait(false);
        try
        {
            // Read current whitelist (no admin required — IOCTL read).
            // Only invoke the CLI write (which requires admin) if the entry is absent.
            var paths = _controller.ApplicationPaths;
            if (paths.Contains(_ownExePath, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogDebug("HidHide: own executable already whitelisted — no write needed '{Path}'", _ownExePath);
                return;
            }

            _controller.AddApplicationPath(_ownExePath);
            _logger.LogInformation("HidHide: whitelisted own executable '{Path}'", _ownExePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HidHide: failed to whitelist own executable at startup");
        }
    }

    // ── Pipeline event handlers ───────────────────────────────────────────────

    private void OnPipelineStarted(object? sender, EventArgs e)
    {
        if (!_settingsService.Settings.AutoHideOnPipelineRun)
            return;

        _ = ApplyAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "HidHide: Apply on pipeline start failed"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void OnPipelineStopped(object? sender, EventArgs e)
    {
        if (!_settingsService.Settings.AutoHideOnPipelineRun)
            return;

        _ = RevertAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "HidHide: Revert on pipeline stop failed"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    // ── Status helpers ────────────────────────────────────────────────────────

    private void UpdateStatus()
    {
        var settings = _settingsService.Settings;
        if (!settings.EnableHidHide)
        {
            SetStatus(HidHideStatus.Disabled);
            return;
        }

        if (!_controller.IsInstalled)
        {
            SetStatus(HidHideStatus.NotInstalled);
            return;
        }

        SetStatus(IsApplied ? HidHideStatus.Active : HidHideStatus.Ready);
    }

    private void SetStatus(HidHideStatus status)
    {
        if (Status == status)
            return;

        Status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
