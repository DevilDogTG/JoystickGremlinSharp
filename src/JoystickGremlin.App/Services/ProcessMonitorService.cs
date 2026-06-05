// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Threading;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Pipeline;
using JoystickGremlin.Core.ProcessMonitor;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.App.Services;

/// <summary>
/// Orchestrates automatic profile loading and pipeline start/stop based on
/// <see cref="IProcessMonitor.ForegroundProcessChanged"/> events and the global
/// trigger list in <see cref="AppSettings.AutoLoadTriggers"/>.
/// </summary>
public sealed class ProcessMonitorService : IDisposable
{
    private readonly IProcessMonitor _processMonitor;
    private readonly ISettingsService _settingsService;
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileState _profileState;
    private readonly IEventPipeline _eventPipeline;
    private readonly ILogger<ProcessMonitorService> _logger;

    private AutoLoadTrigger? _activeTriggerWithPipeline;

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessMonitorService"/>.
    /// </summary>
    public ProcessMonitorService(
        IProcessMonitor processMonitor,
        ISettingsService settingsService,
        IProfileRepository profileRepository,
        IProfileState profileState,
        IEventPipeline eventPipeline,
        ILogger<ProcessMonitorService> logger)
    {
        _processMonitor    = processMonitor;
        _settingsService   = settingsService;
        _profileRepository = profileRepository;
        _profileState      = profileState;
        _eventPipeline     = eventPipeline;
        _logger            = logger;

        _processMonitor.ForegroundProcessChanged += OnForegroundProcessChanged;
    }

    /// <summary>
    /// Starts the underlying <see cref="IProcessMonitor"/> so it begins watching the foreground window.
    /// </summary>
    public void Start() => _processMonitor.Start();

    /// <summary>
    /// Stops the underlying <see cref="IProcessMonitor"/>.
    /// </summary>
    public void Stop() => _processMonitor.Stop();

    /// <inheritdoc/>
    public void Dispose()
    {
        _processMonitor.ForegroundProcessChanged -= OnForegroundProcessChanged;
        _processMonitor.Dispose();
    }

    private void OnForegroundProcessChanged(object? sender, string executablePath)
    {
        if (!_settingsService.Settings.EnableAutoLoading) return;

        var trigger = ProcessProfileResolver.Resolve(
            executablePath, _settingsService.Settings.AutoLoadTriggers);

        if (trigger is not null)
        {
            _ = HandleTriggerActivatedAsync(trigger);
        }
        else
        {
            HandleTriggerDeactivated();
        }
    }

    private async Task HandleTriggerActivatedAsync(AutoLoadTrigger trigger)
    {
        var profilePath = trigger.ProfilePath;

        // A trigger saved before its profile was picked has no path yet — nothing to load.
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return;
        }

        // Skip if same profile is already loaded.
        var currentPath = _profileState.FilePath;
        if (string.Equals(currentPath, profilePath, StringComparison.OrdinalIgnoreCase)
            && _eventPipeline.IsRunning)
        {
            return;
        }

        try
        {
            _logger.LogInformation(
                "Auto-loading profile '{Profile}' triggered by '{Match}'.",
                profilePath,
                trigger.MatchType == ProcessMatchType.ExecutableName
                    ? trigger.ExecutableName
                    : trigger.ExecutablePath);

            var profile = await _profileRepository.LoadAsync(profilePath);
            Dispatcher.UIThread.Post(() =>
            {
                _profileState.SetProfile(profile, profilePath);

                if (trigger.AutoStart && !_eventPipeline.IsRunning)
                {
                    _eventPipeline.Start(profile);
                    _activeTriggerWithPipeline = trigger;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-load profile '{Profile}'.", profilePath);
        }
    }

    private void HandleTriggerDeactivated()
    {
        if (_activeTriggerWithPipeline is null) return;
        if (_activeTriggerWithPipeline.RemainActiveOnFocusLoss) return;

        _logger.LogInformation("Triggered process left foreground; stopping pipeline.");
        Dispatcher.UIThread.Post(() =>
        {
            _eventPipeline.Stop();
            _activeTriggerWithPipeline = null;
        });
    }
}
