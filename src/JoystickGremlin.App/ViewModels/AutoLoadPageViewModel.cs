// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
/// ViewModel for the Auto-load page. Shows one group per profile in the library, each
/// containing that profile's <see cref="ProcessTrigger"/> list. Edits are persisted by
/// writing back to each modified profile's JSON file via <see cref="IProfileRepository"/>.
/// The global on/off switch is still backed by <see cref="AppSettings.EnableAutoLoading"/>.
/// </summary>
public sealed class AutoLoadPageViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IProfileLibrary _profileLibrary;
    private readonly IProfileRepository _profileRepository;
    private readonly IProcessPickerDialogService _processPicker;
    private readonly IFilePickerService _filePicker;
    private readonly ILogger<AutoLoadPageViewModel> _logger;

    private readonly Subject<Unit> _saveTrigger = new();
    private readonly CompositeDisposable _subscriptions = [];

    private bool _enableAutoLoading;
    private bool _loading;

    /// <summary>
    /// Initializes a new instance of <see cref="AutoLoadPageViewModel"/>.
    /// </summary>
    public AutoLoadPageViewModel(
        ISettingsService settingsService,
        IProfileLibrary profileLibrary,
        IProfileRepository profileRepository,
        IProcessPickerDialogService processPicker,
        IFilePickerService filePicker,
        ILogger<AutoLoadPageViewModel> logger)
    {
        _settingsService   = settingsService;
        _profileLibrary    = profileLibrary;
        _profileRepository = profileRepository;
        _processPicker     = processPicker;
        _filePicker        = filePicker;
        _logger            = logger;

        ProfileGroups = [];

        _subscriptions.Add(
            _saveTrigger
                .Throttle(TimeSpan.FromMilliseconds(800), AvaloniaScheduler.Instance)
                .Subscribe(unit =>
                {
                    if (!_loading) _ = SaveAsync();
                }));

        _profileLibrary.LibraryChanged += OnLibraryChanged;
    }

    /// <summary>Gets or sets whether the auto-load feature is globally enabled.</summary>
    public bool EnableAutoLoading
    {
        get => _enableAutoLoading;
        set
        {
            this.RaiseAndSetIfChanged(ref _enableAutoLoading, value);
            if (!_loading)
                _ = SaveSettingsAsync();
        }
    }

    /// <summary>Gets the per-profile trigger groups, one per profile in the library.</summary>
    public ObservableCollection<ProfileTriggersGroupViewModel> ProfileGroups { get; }

    /// <summary>
    /// Populates the page from the current settings and profile library.
    /// Call after settings have loaded and the profile library has been scanned.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            EnableAutoLoading = _settingsService.Settings.EnableAutoLoading;
            RebuildGroups();
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Refreshes the groups list when the profile library changes (a profile was added,
    /// renamed, or deleted). Marshalled to the UI thread; suppresses save side effects.
    /// </summary>
    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _loading = true;
            try
            {
                RebuildGroups();
            }
            finally
            {
                _loading = false;
            }
        });
    }

    private void RebuildGroups()
    {
        // Dispose the existing groups before clearing so their per-row reactive
        // subscriptions are released. Without this, every library scan would
        // accumulate detached subscriptions until GC eventually collects them.
        foreach (var oldGroup in ProfileGroups)
            oldGroup.Dispose();
        ProfileGroups.Clear();

        foreach (var entry in _profileLibrary.Entries)
        {
            ProfileGroups.Add(new ProfileTriggersGroupViewModel(
                entry,
                entry.AutoLoadTriggers,
                _processPicker,
                _filePicker,
                ScheduleSave));
        }
    }

    private void ScheduleSave()
    {
        if (!_loading)
            _saveTrigger.OnNext(Unit.Default);
    }

    private async Task SaveAsync()
    {
        // Persist any per-profile edits.
        var dirtyGroups = ProfileGroups.Where(g => g.IsDirty).ToList();
        var savedAtLeastOne = false;
        foreach (var group in dirtyGroups)
        {
            try
            {
                var profile = await _profileRepository.LoadAsync(group.Profile.FilePath);

                // Replace the in-memory triggers list and save back. SnapshotTriggers
                // is pure — the dirty flag is only cleared via MarkSaved() below,
                // and only after the save succeeds, so a failed save stays in the
                // dirty queue for the next debounced retry.
                profile.AutoLoadTriggers.Clear();
                profile.AutoLoadTriggers.AddRange(group.SnapshotTriggers());

                await _profileRepository.SaveAsync(profile, group.Profile.FilePath);
                group.MarkSaved();
                savedAtLeastOne = true;
                _logger.LogInformation(
                    "Saved {Count} auto-load trigger(s) to profile '{Path}'",
                    profile.AutoLoadTriggers.Count, group.Profile.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to save auto-load triggers for profile '{Path}' — will retry on next change",
                    group.Profile.FilePath);
                // IsDirty stays true; the next debounced save retries this group.
            }
        }

        if (savedAtLeastOne)
        {
            // Refresh the library so the in-memory snapshot used by ProcessMonitorService
            // (via IProfileLibrary.Entries) reflects the just-saved triggers.
            try
            {
                await _profileLibrary.ScanAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh profile library after saving triggers");
            }
        }

        // Persist the global enable flag if it changed.
        await SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            if (_settingsService.Settings.EnableAutoLoading == EnableAutoLoading)
                return;

            _settingsService.Settings.EnableAutoLoading = EnableAutoLoading;
            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save auto-load enable flag");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _profileLibrary.LibraryChanged -= OnLibraryChanged;
        foreach (var group in ProfileGroups)
            group.Dispose();
        ProfileGroups.Clear();
        _subscriptions.Dispose();
        _saveTrigger.Dispose();
    }
}
