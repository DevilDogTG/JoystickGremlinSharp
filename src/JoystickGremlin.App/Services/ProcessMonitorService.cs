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
/// <see cref="IProcessMonitor.ForegroundProcessChanged"/> events and
/// the user's <see cref="AppSettings.ProcessMappings"/> configuration.
/// </summary>
public sealed class ProcessMonitorService : IDisposable
{
    private readonly IProcessMonitor _processMonitor;
    private readonly ISettingsService _settingsService;
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileState _profileState;
    private readonly IEventPipeline _eventPipeline;
    private readonly ILogger<ProcessMonitorService> _logger;

    private ProcessProfileMapping? _activeMappingWithPipeline;

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
        var settings = _settingsService.Settings;
        if (!settings.EnableAutoLoading) return;

        var mapping = ProcessProfileResolver.Resolve(executablePath, settings.ProcessMappings);

        if (mapping is not null)
        {
            // A mapped game is now in the foreground.
            _ = HandleMappingActivatedAsync(mapping);
        }
        else
        {
            // No mapped game is in the foreground.
            HandleMappingDeactivated();
        }
    }

    private async Task HandleMappingActivatedAsync(ProcessProfileMapping mapping)
    {
        // Skip if same profile is already loaded.
        var currentPath = _profileState.FilePath;
        if (string.Equals(currentPath, mapping.ProfilePath, StringComparison.OrdinalIgnoreCase)
            && _eventPipeline.IsRunning)
        {
            return;
        }

        try
        {
            _logger.LogInformation(
                "Auto-loading profile '{Profile}' for executable match.", mapping.ProfilePath);

            var profile = await _profileRepository.LoadAsync(mapping.ProfilePath);
            Dispatcher.UIThread.Post(() =>
            {
                _profileState.SetProfile(profile, mapping.ProfilePath);

                if (mapping.AutoStart && !_eventPipeline.IsRunning)
                {
                    _eventPipeline.Start(profile);
                    _activeMappingWithPipeline = mapping;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-load profile '{Profile}'.", mapping.ProfilePath);
        }
    }

    private void HandleMappingDeactivated()
    {
        if (_activeMappingWithPipeline is null) return;
        if (_activeMappingWithPipeline.RemainActiveOnFocusLoss) return;

        _logger.LogInformation("Mapped process left foreground; stopping pipeline.");
        Dispatcher.UIThread.Post(() =>
        {
            _eventPipeline.Stop();
            _activeMappingWithPipeline = null;
        });
    }
}
