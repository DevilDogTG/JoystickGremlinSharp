// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ForceFeedback;
using JoystickGremlin.Core.Startup;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Settings page — exposes application-level
/// configuration options backed by <see cref="AppSettings"/>.
/// Changes are auto-saved after an 800 ms debounce.
/// </summary>
public sealed class SettingsPageViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly IFilePickerService _filePicker;
    private readonly IForceFeedbackBridge _ffbBridge;
    private readonly ILogger<SettingsPageViewModel> _logger;
    private string _profilesFolderPath = string.Empty;
    private bool _startMinimized;
    private bool _startWithWindows;
    private bool _closeToTray = true;
    private bool _enableAutoLoading;
    private bool _enableFfbBridge;
    private decimal _ffbVJoyDeviceId = 1;
    private int _ffbGainPercent = 100;
    private string _ffbWheelInstanceGuid = string.Empty;
    private string _ffbBridgeStatus = "Disabled";
    private int _uiUpdateIntervalMs = 10;
    private bool _loading;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsPageViewModel"/>.
    /// </summary>
    public SettingsPageViewModel(
        ISettingsService settingsService,
        IStartupService startupService,
        IFilePickerService filePicker,
        IForceFeedbackBridge ffbBridge,
        ILogger<SettingsPageViewModel> logger)
    {
        _settingsService = settingsService;
        _startupService  = startupService;
        _filePicker      = filePicker;
        _ffbBridge       = ffbBridge;
        _logger          = logger;

        ProcessMappings = [];
        AddMappingCommand    = ReactiveCommand.Create(AddMapping);
        RemoveMappingCommand = ReactiveCommand.Create<ProcessMappingViewModel>(RemoveMapping);
        MoveUpCommand        = ReactiveCommand.Create<ProcessMappingViewModel>(MoveUp);
        MoveDownCommand      = ReactiveCommand.Create<ProcessMappingViewModel>(MoveDown);

        _ffbBridge.StateChanged += OnBridgeStateChanged;
        _ffbBridgeStatus = _ffbBridge.State.ToString();

        this.WhenAnyValue(
                x => x.ProfilesFolderPath,
                x => x.StartMinimized,
                x => x.StartWithWindows,
                x => x.CloseToTray,
                x => x.EnableAutoLoading,
                x => x.UiUpdateIntervalMs,
                (_, _, _, _, _, _) => Unit.Default)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(800), AvaloniaScheduler.Instance)
            .Subscribe(unit => { if (!_loading) _ = SaveAsync(); });

        this.WhenAnyValue(
                x => x.EnableFfbBridge,
                x => x.FfbVJoyDeviceId,
                x => x.FfbGainPercent,
                x => x.FfbWheelInstanceGuid,
                (_, _, _, _) => Unit.Default)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(800), AvaloniaScheduler.Instance)
            .Subscribe(unit => { if (!_loading) _ = SaveAsync(); });
    }

    /// <summary>Gets or sets the folder path where profiles are stored.</summary>
    public string ProfilesFolderPath
    {
        get => _profilesFolderPath;
        set => this.RaiseAndSetIfChanged(ref _profilesFolderPath, value);
    }

    /// <summary>Gets or sets whether the application should start minimized to the system tray.</summary>
    public bool StartMinimized
    {
        get => _startMinimized;
        set => this.RaiseAndSetIfChanged(ref _startMinimized, value);
    }

    /// <summary>Gets or sets whether the application is registered to start with Windows.</summary>
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => this.RaiseAndSetIfChanged(ref _startWithWindows, value);
    }

    /// <summary>Gets or sets whether closing the window minimizes to tray instead of exiting.</summary>
    public bool CloseToTray
    {
        get => _closeToTray;
        set => this.RaiseAndSetIfChanged(ref _closeToTray, value);
    }

    /// <summary>Gets or sets whether the auto-load feature is globally enabled.</summary>
    public bool EnableAutoLoading
    {
        get => _enableAutoLoading;
        set => this.RaiseAndSetIfChanged(ref _enableAutoLoading, value);
    }

    /// <summary>
    /// Gets or sets the live-input UI update interval in milliseconds (1–1000).
    /// Lower values increase refresh frequency but consume more CPU.
    /// </summary>
    public int UiUpdateIntervalMs
    {
        get => _uiUpdateIntervalMs;
        set
        {
            this.RaiseAndSetIfChanged(ref _uiUpdateIntervalMs, value);
            this.RaisePropertyChanged(nameof(UiUpdateHz));
            this.RaisePropertyChanged(nameof(UiUpdateHighFrequencyWarning));
        }
    }

    /// <summary>Gets the computed refresh rate in Hz based on <see cref="UiUpdateIntervalMs"/>.</summary>
    public string UiUpdateHz => _uiUpdateIntervalMs > 0
        ? $"{1000.0 / _uiUpdateIntervalMs:F0} Hz"
        : "∞";

    /// <summary>Gets a value indicating whether the current interval is high-frequency (≤ 5 ms).</summary>
    public bool UiUpdateHighFrequencyWarning => _uiUpdateIntervalMs is > 0 and <= 5;

    /// <summary>Gets or sets whether the force feedback bridge is enabled.</summary>
    public bool EnableFfbBridge
    {
        get => _enableFfbBridge;
        set => this.RaiseAndSetIfChanged(ref _enableFfbBridge, value);
    }

    /// <summary>Gets or sets the vJoy device ID that the FFB bridge listens to (1–16).</summary>
    public decimal FfbVJoyDeviceId
    {
        get => _ffbVJoyDeviceId;
        set => this.RaiseAndSetIfChanged(ref _ffbVJoyDeviceId, value);
    }

    /// <summary>Gets or sets the FFB output gain percentage (0–200, where 100 = pass-through).</summary>
    public int FfbGainPercent
    {
        get => _ffbGainPercent;
        set => this.RaiseAndSetIfChanged(ref _ffbGainPercent, value);
    }

    /// <summary>
    /// Gets or sets the DirectInput instance GUID of the target wheel device.
    /// Leave empty to auto-detect the first MOZA device found.
    /// </summary>
    public string FfbWheelInstanceGuid
    {
        get => _ffbWheelInstanceGuid;
        set => this.RaiseAndSetIfChanged(ref _ffbWheelInstanceGuid, value);
    }

    /// <summary>Gets the current operational state of the force feedback bridge (read-only, live).</summary>
    public string FfbBridgeStatus
    {
        get => _ffbBridgeStatus;
        private set => this.RaiseAndSetIfChanged(ref _ffbBridgeStatus, value);
    }

    /// <summary>Gets the ordered list of process-to-profile mapping ViewModels.</summary>
    public ObservableCollection<ProcessMappingViewModel> ProcessMappings { get; }

    /// <summary>Gets the command that adds a new empty process mapping entry.</summary>
    public ReactiveCommand<Unit, Unit> AddMappingCommand { get; }

    /// <summary>Gets the command that removes the given mapping entry.</summary>
    public ReactiveCommand<ProcessMappingViewModel, Unit> RemoveMappingCommand { get; }

    /// <summary>Gets the command that moves the given mapping entry up (higher priority).</summary>
    public ReactiveCommand<ProcessMappingViewModel, Unit> MoveUpCommand { get; }

    /// <summary>Gets the command that moves the given mapping entry down (lower priority).</summary>
    public ReactiveCommand<ProcessMappingViewModel, Unit> MoveDownCommand { get; }

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
            ProfilesFolderPath   = s.ProfilesFolderPath ?? string.Empty;
            StartMinimized       = s.StartMinimized;
            StartWithWindows     = _startupService.IsEnabled;
            CloseToTray          = s.CloseToTray;
            EnableAutoLoading    = s.EnableAutoLoading;
            UiUpdateIntervalMs   = s.UiUpdateIntervalMs > 0 ? s.UiUpdateIntervalMs : 10;
            EnableFfbBridge      = s.EnableFfbBridge;
            FfbVJoyDeviceId      = s.FfbVJoyDeviceId;
            FfbGainPercent       = s.FfbGainPercent;
            FfbWheelInstanceGuid = s.FfbWheelInstanceGuid ?? string.Empty;

            ProcessMappings.Clear();
            foreach (var m in s.ProcessMappings)
                ProcessMappings.Add(new ProcessMappingViewModel(m, _filePicker));
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnBridgeStateChanged(object? sender, ForceFeedbackBridgeState state)
    {
        // StateChanged can fire on the vJoy native callback thread; dispatch to UI thread
        // before updating the ReactiveUI property, which drives Avalonia bindings.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => FfbBridgeStatus = state.ToString());
    }

    private void AddMapping()
    {
        var model = new ProcessProfileMapping();
        _settingsService.Settings.ProcessMappings.Add(model);
        ProcessMappings.Add(new ProcessMappingViewModel(model, _filePicker));
        _ = SaveAsync();
    }

    private void RemoveMapping(ProcessMappingViewModel vm)
    {
        _settingsService.Settings.ProcessMappings.Remove(vm.Model);
        ProcessMappings.Remove(vm);
        _ = SaveAsync();
    }

    private void MoveUp(ProcessMappingViewModel vm)
    {
        var idx = ProcessMappings.IndexOf(vm);
        if (idx <= 0) return;
        ProcessMappings.Move(idx, idx - 1);
        var list = _settingsService.Settings.ProcessMappings;
        var tmp = list[idx]; list[idx] = list[idx - 1]; list[idx - 1] = tmp;
        _ = SaveAsync();
    }

    private void MoveDown(ProcessMappingViewModel vm)
    {
        var idx = ProcessMappings.IndexOf(vm);
        if (idx < 0 || idx >= ProcessMappings.Count - 1) return;
        ProcessMappings.Move(idx, idx + 1);
        var list = _settingsService.Settings.ProcessMappings;
        var tmp = list[idx]; list[idx] = list[idx + 1]; list[idx + 1] = tmp;
        _ = SaveAsync();
    }

    private async Task SaveAsync()
    {
        // Flush all mapping ViewModels to their underlying models first.
        foreach (var vm in ProcessMappings)
            vm.ApplyToModel();

        var s = _settingsService.Settings;
        s.ProfilesFolderPath = string.IsNullOrWhiteSpace(ProfilesFolderPath) ? null : ProfilesFolderPath;
        s.StartMinimized     = StartMinimized;
        s.CloseToTray        = CloseToTray;
        s.EnableAutoLoading  = EnableAutoLoading;
        s.UiUpdateIntervalMs = UiUpdateIntervalMs;
        s.EnableFfbBridge    = EnableFfbBridge;
        s.FfbVJoyDeviceId    = (uint)FfbVJoyDeviceId;
        s.FfbGainPercent     = FfbGainPercent;
        s.FfbWheelInstanceGuid = string.IsNullOrWhiteSpace(FfbWheelInstanceGuid) ? null : FfbWheelInstanceGuid;

        // Sync the startup registry entry with the toggle value.
        if (StartWithWindows && !_startupService.IsEnabled)
        {
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
                _startupService.Enable(exePath);
        }
        else if (!StartWithWindows && _startupService.IsEnabled)
        {
            _startupService.Disable();
        }

        s.StartWithWindows = StartWithWindows;
        // ProcessMappings list is already mutated in-place by AddMapping/RemoveMapping/Move*.
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
