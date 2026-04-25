// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
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
    private readonly IFilePickerService _filePicker;
    private readonly ILogger<SettingsPageViewModel> _logger;
    private string _lastProfilePath = string.Empty;
    private decimal _vJoyDeviceId = 1;
    private string _defaultModeName = string.Empty;
    private bool _startMinimized;
    private bool _enableAutoLoading;
    private bool _loading;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsPageViewModel"/>.
    /// </summary>
    public SettingsPageViewModel(
        ISettingsService settingsService,
        IFilePickerService filePicker,
        ILogger<SettingsPageViewModel> logger)
    {
        _settingsService = settingsService;
        _filePicker      = filePicker;
        _logger          = logger;

        ProcessMappings = [];
        AddMappingCommand    = ReactiveCommand.Create(AddMapping);
        RemoveMappingCommand = ReactiveCommand.Create<ProcessMappingViewModel>(RemoveMapping);
        MoveUpCommand        = ReactiveCommand.Create<ProcessMappingViewModel>(MoveUp);
        MoveDownCommand      = ReactiveCommand.Create<ProcessMappingViewModel>(MoveDown);

        this.WhenAnyValue(
                x => x.LastProfilePath,
                x => x.VJoyDeviceId,
                x => x.DefaultModeName,
                x => x.StartMinimized,
                x => x.EnableAutoLoading,
                (_, _, _, _, _) => Unit.Default)
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

    /// <summary>Gets or sets whether the auto-load feature is globally enabled.</summary>
    public bool EnableAutoLoading
    {
        get => _enableAutoLoading;
        set => this.RaiseAndSetIfChanged(ref _enableAutoLoading, value);
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
            LastProfilePath  = s.LastProfilePath ?? string.Empty;
            VJoyDeviceId     = s.VJoyDeviceId;
            DefaultModeName  = s.DefaultModeName ?? string.Empty;
            StartMinimized   = s.StartMinimized;
            EnableAutoLoading = s.EnableAutoLoading;

            ProcessMappings.Clear();
            foreach (var m in s.ProcessMappings)
                ProcessMappings.Add(new ProcessMappingViewModel(m, _filePicker));
        }
        finally
        {
            _loading = false;
        }
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
        s.LastProfilePath    = string.IsNullOrWhiteSpace(LastProfilePath) ? null : LastProfilePath;
        s.VJoyDeviceId       = (uint)VJoyDeviceId;
        s.DefaultModeName    = string.IsNullOrWhiteSpace(DefaultModeName) ? null : DefaultModeName;
        s.StartMinimized     = StartMinimized;
        s.EnableAutoLoading  = EnableAutoLoading;
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
