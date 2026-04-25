// SPDX-License-Identifier: GPL-3.0-only

using System.Reactive;
using System.Reactive.Linq;
using JoystickGremlin.Core.Configuration;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Settings page — exposes application-level
/// configuration options backed by <see cref="AppSettings"/>.
/// Changes are auto-saved after an 800 ms debounce.
/// </summary>
public sealed class SettingsPageViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsPageViewModel> _logger;
    private string _lastProfilePath = string.Empty;
    private decimal _vJoyDeviceId = 1;
    private string _defaultModeName = string.Empty;
    private bool _startMinimized;
    private bool _loading;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsPageViewModel"/>.
    /// </summary>
    public SettingsPageViewModel(ISettingsService settingsService, ILogger<SettingsPageViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        this.WhenAnyValue(
                x => x.LastProfilePath,
                x => x.VJoyDeviceId,
                x => x.DefaultModeName,
                x => x.StartMinimized,
                (_, _, _, _) => Unit.Default)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(800), RxApp.MainThreadScheduler)
            .Subscribe(unit => { if (!_loading) _ = SaveAsync(); });
    }

    /// <summary>Gets or sets the path to the last opened profile file.</summary>
    public string LastProfilePath
    {
        get => _lastProfilePath;
        set => this.RaiseAndSetIfChanged(ref _lastProfilePath, value);
    }

    /// <summary>Gets or sets the vJoy device ID (1–16).</summary>
    public decimal VJoyDeviceId
    {
        get => _vJoyDeviceId;
        set => this.RaiseAndSetIfChanged(ref _vJoyDeviceId, value);
    }

    /// <summary>Gets or sets the name of the default mode activated on startup.</summary>
    public string DefaultModeName
    {
        get => _defaultModeName;
        set => this.RaiseAndSetIfChanged(ref _defaultModeName, value);
    }

    /// <summary>Gets or sets whether the application should start minimized to the system tray.</summary>
    public bool StartMinimized
    {
        get => _startMinimized;
        set => this.RaiseAndSetIfChanged(ref _startMinimized, value);
    }

    /// <summary>
    /// Populates ViewModel properties from the current <see cref="ISettingsService.Settings"/>.
    /// Call after <see cref="ISettingsService.LoadAsync"/> completes.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            var s = _settingsService.Settings;
            LastProfilePath = s.LastProfilePath ?? string.Empty;
            VJoyDeviceId = s.VJoyDeviceId;
            DefaultModeName = s.DefaultModeName ?? string.Empty;
            StartMinimized = s.StartMinimized;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveAsync()
    {
        var s = _settingsService.Settings;
        s.LastProfilePath = string.IsNullOrWhiteSpace(LastProfilePath) ? null : LastProfilePath;
        s.VJoyDeviceId = (uint)VJoyDeviceId;
        s.DefaultModeName = string.IsNullOrWhiteSpace(DefaultModeName) ? null : DefaultModeName;
        s.StartMinimized = StartMinimized;
        try
        {
            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }
}
