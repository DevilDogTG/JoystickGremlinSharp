// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia.Threading;
using JoystickGremlin.App.Services;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Auto-load page. Manages the global enable switch and the list of
/// process-to-profile mappings, backed by <see cref="AppSettings"/>. Changes are auto-saved
/// after an 800 ms debounce.
/// </summary>
public sealed class AutoLoadPageViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IProfileLibrary _profileLibrary;
    private readonly IProcessPickerDialogService _processPicker;
    private readonly IFilePickerService _filePicker;
    private readonly ILogger<AutoLoadPageViewModel> _logger;

    private readonly Subject<Unit> _saveTrigger = new();
    private readonly CompositeDisposable _subscriptions = [];
    private readonly Dictionary<ProcessMappingViewModel, IDisposable> _rowSubscriptions = [];

    private bool _enableAutoLoading;
    private bool _loading;

    /// <summary>
    /// Initializes a new instance of <see cref="AutoLoadPageViewModel"/>.
    /// </summary>
    public AutoLoadPageViewModel(
        ISettingsService settingsService,
        IProfileLibrary profileLibrary,
        IProcessPickerDialogService processPicker,
        IFilePickerService filePicker,
        ILogger<AutoLoadPageViewModel> logger)
    {
        _settingsService = settingsService;
        _profileLibrary  = profileLibrary;
        _processPicker   = processPicker;
        _filePicker      = filePicker;
        _logger          = logger;

        Mappings = [];
        AvailableProfiles = [];

        AddMappingCommand    = ReactiveCommand.Create(AddMapping);
        RemoveMappingCommand = ReactiveCommand.Create<ProcessMappingViewModel>(RemoveMapping);
        MoveUpCommand        = ReactiveCommand.Create<ProcessMappingViewModel>(MoveUp);
        MoveDownCommand      = ReactiveCommand.Create<ProcessMappingViewModel>(MoveDown);

        _subscriptions.Add(
            _saveTrigger
                .Throttle(TimeSpan.FromMilliseconds(800), AvaloniaScheduler.Instance)
                .Subscribe(unit => { if (!_loading) _ = SaveAsync(); }));

        _profileLibrary.LibraryChanged += OnLibraryChanged;
    }

    /// <summary>Gets or sets whether the auto-load feature is globally enabled.</summary>
    public bool EnableAutoLoading
    {
        get => _enableAutoLoading;
        set
        {
            this.RaiseAndSetIfChanged(ref _enableAutoLoading, value);
            ScheduleSave();
        }
    }

    /// <summary>Gets the ordered list of process-to-profile mapping ViewModels.</summary>
    public ObservableCollection<ProcessMappingViewModel> Mappings { get; }

    /// <summary>Gets the profiles selectable in each mapping's profile dropdown.</summary>
    public ObservableCollection<ProfileEntry> AvailableProfiles { get; }

    /// <summary>Gets the command that adds a new empty mapping entry.</summary>
    public ReactiveCommand<Unit, Unit> AddMappingCommand { get; }

    /// <summary>Gets the command that removes the given mapping entry.</summary>
    public ReactiveCommand<ProcessMappingViewModel, Unit> RemoveMappingCommand { get; }

    /// <summary>Gets the command that moves the given mapping entry up (higher priority).</summary>
    public ReactiveCommand<ProcessMappingViewModel, Unit> MoveUpCommand { get; }

    /// <summary>Gets the command that moves the given mapping entry down (lower priority).</summary>
    public ReactiveCommand<ProcessMappingViewModel, Unit> MoveDownCommand { get; }

    /// <summary>
    /// Populates the page from the current settings and profile library.
    /// Call after settings have loaded and the profile library has been scanned.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            RebuildProfiles();

            var s = _settingsService.Settings;
            EnableAutoLoading = s.EnableAutoLoading;

            ClearRows();
            foreach (var model in s.ProcessMappings)
                AddRow(new ProcessMappingViewModel(model, _processPicker, _filePicker, AvailableProfiles));
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _loading = true;
            try
            {
                RebuildProfiles();
                foreach (var row in Mappings)
                    row.RefreshSelectedProfile();
            }
            finally
            {
                _loading = false;
            }
        });
    }

    private void RebuildProfiles()
    {
        AvailableProfiles.Clear();
        foreach (var entry in _profileLibrary.Entries)
            AvailableProfiles.Add(entry);
    }

    private void AddMapping()
    {
        var model = new ProcessProfileMapping();
        _settingsService.Settings.ProcessMappings.Add(model);
        AddRow(new ProcessMappingViewModel(model, _processPicker, _filePicker, AvailableProfiles));
        ScheduleSave();
    }

    private void RemoveMapping(ProcessMappingViewModel vm)
    {
        _settingsService.Settings.ProcessMappings.Remove(vm.Model);
        RemoveRow(vm);
        ScheduleSave();
    }

    private void MoveUp(ProcessMappingViewModel vm)
    {
        var idx = Mappings.IndexOf(vm);
        if (idx <= 0) return;
        Mappings.Move(idx, idx - 1);
        var list = _settingsService.Settings.ProcessMappings;
        (list[idx], list[idx - 1]) = (list[idx - 1], list[idx]);
        ScheduleSave();
    }

    private void MoveDown(ProcessMappingViewModel vm)
    {
        var idx = Mappings.IndexOf(vm);
        if (idx < 0 || idx >= Mappings.Count - 1) return;
        Mappings.Move(idx, idx + 1);
        var list = _settingsService.Settings.ProcessMappings;
        (list[idx], list[idx + 1]) = (list[idx + 1], list[idx]);
        ScheduleSave();
    }

    private void AddRow(ProcessMappingViewModel row)
    {
        Mappings.Add(row);
        _rowSubscriptions[row] = row.Changed.Subscribe(_ => ScheduleSave());
    }

    private void RemoveRow(ProcessMappingViewModel row)
    {
        if (_rowSubscriptions.Remove(row, out var sub))
            sub.Dispose();
        Mappings.Remove(row);
    }

    private void ClearRows()
    {
        foreach (var sub in _rowSubscriptions.Values)
            sub.Dispose();
        _rowSubscriptions.Clear();
        Mappings.Clear();
    }

    private void ScheduleSave()
    {
        if (!_loading)
            _saveTrigger.OnNext(Unit.Default);
    }

    private async Task SaveAsync()
    {
        foreach (var row in Mappings)
            row.ApplyToModel();

        _settingsService.Settings.EnableAutoLoading = EnableAutoLoading;
        // The ProcessMappings list is mutated in place by Add/Remove/Move.

        try
        {
            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save auto-load settings");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _profileLibrary.LibraryChanged -= OnLibraryChanged;
        foreach (var sub in _rowSubscriptions.Values)
            sub.Dispose();
        _rowSubscriptions.Clear();
        _subscriptions.Dispose();
        _saveTrigger.Dispose();
    }
}
