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
using JoystickGremlin.Core.ProcessMonitor;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Auto-load page. Shows the global trigger list from
/// <see cref="AppSettings.AutoLoadTriggers"/> as one flat table; each row picks its
/// target profile from the library. Edits are debounced and persisted to
/// <c>settings.json</c>. When profile files still carrying legacy embedded triggers
/// are detected (e.g. copied in from an older installation), a banner offers a manual
/// migration via <see cref="IAutoLoadTriggerMigrator"/>.
/// </summary>
public sealed class AutoLoadPageViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IProfileLibrary _profileLibrary;
    private readonly IAutoLoadTriggerMigrator _migrator;
    private readonly IProcessPickerDialogService _processPicker;
    private readonly IFilePickerService _filePicker;
    private readonly ILogger<AutoLoadPageViewModel> _logger;

    private readonly Subject<Unit> _saveTrigger = new();
    private readonly CompositeDisposable _subscriptions = [];
    private readonly Dictionary<ProcessTriggerViewModel, IDisposable> _rowSubscriptions = [];

    private bool _enableAutoLoading;
    private bool _loading;
    private bool _isDirty;
    private int _legacyProfileCount;
    private string? _migrationStatusMessage;
    private bool _isMigrating;

    /// <summary>
    /// Initializes a new instance of <see cref="AutoLoadPageViewModel"/>.
    /// </summary>
    public AutoLoadPageViewModel(
        ISettingsService settingsService,
        IProfileLibrary profileLibrary,
        IAutoLoadTriggerMigrator migrator,
        IProcessPickerDialogService processPicker,
        IFilePickerService filePicker,
        ILogger<AutoLoadPageViewModel> logger)
    {
        _settingsService = settingsService;
        _profileLibrary  = profileLibrary;
        _migrator        = migrator;
        _processPicker   = processPicker;
        _filePicker      = filePicker;
        _logger          = logger;

        Triggers = [];
        AvailableProfiles = [];

        _subscriptions.Add(
            _saveTrigger
                .Throttle(TimeSpan.FromMilliseconds(800), AvaloniaScheduler.Instance)
                .Subscribe(unit =>
                {
                    if (!_loading)
                    {
                        _ = SaveAsync();
                    }
                }));

        AddTriggerCommand    = ReactiveCommand.Create(AddTrigger);
        RemoveTriggerCommand = ReactiveCommand.Create<ProcessTriggerViewModel>(RemoveTrigger);
        MoveUpCommand        = ReactiveCommand.Create<ProcessTriggerViewModel>(MoveUp);
        MoveDownCommand      = ReactiveCommand.Create<ProcessTriggerViewModel>(MoveDown);
        MigrateLegacyCommand = ReactiveCommand.CreateFromTask(
            MigrateLegacyAsync,
            this.WhenAnyValue(x => x.IsMigrating, migrating => !migrating));

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
            {
                _ = SaveSettingsAsync();
            }
        }
    }

    /// <summary>Gets the global trigger rows, in evaluation order (first match wins).</summary>
    public ObservableCollection<ProcessTriggerViewModel> Triggers { get; }

    /// <summary>Gets the library profiles selectable in each row's profile dropdown.</summary>
    public ObservableCollection<ProfileEntry> AvailableProfiles { get; }

    /// <summary>Gets the command that appends a new blank trigger row.</summary>
    public ReactiveCommand<Unit, Unit> AddTriggerCommand { get; }

    /// <summary>Gets the command that removes a trigger row.</summary>
    public ReactiveCommand<ProcessTriggerViewModel, Unit> RemoveTriggerCommand { get; }

    /// <summary>Gets the command that moves the given trigger up (higher priority).</summary>
    public ReactiveCommand<ProcessTriggerViewModel, Unit> MoveUpCommand { get; }

    /// <summary>Gets the command that moves the given trigger down (lower priority).</summary>
    public ReactiveCommand<ProcessTriggerViewModel, Unit> MoveDownCommand { get; }

    /// <summary>Gets the command that lifts legacy profile-embedded triggers into the global list.</summary>
    public ReactiveCommand<Unit, Unit> MigrateLegacyCommand { get; }

    /// <summary>Gets the number of profile files still carrying legacy embedded triggers.</summary>
    public int LegacyProfileCount
    {
        get => _legacyProfileCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _legacyProfileCount, value);
            this.RaisePropertyChanged(nameof(HasLegacyTriggers));
            this.RaisePropertyChanged(nameof(LegacyBannerText));
        }
    }

    /// <summary>Gets a value indicating whether the legacy-trigger migration banner is shown.</summary>
    public bool HasLegacyTriggers => _legacyProfileCount > 0;

    /// <summary>Gets the banner text describing the detected legacy triggers.</summary>
    public string LegacyBannerText =>
        $"{_legacyProfileCount} profile{(_legacyProfileCount == 1 ? string.Empty : "s")} " +
        "contain legacy embedded auto-load triggers.";

    /// <summary>Gets the result message of the last manual migration, or <c>null</c>.</summary>
    public string? MigrationStatusMessage
    {
        get => _migrationStatusMessage;
        private set => this.RaiseAndSetIfChanged(ref _migrationStatusMessage, value);
    }

    /// <summary>Gets a value indicating whether a manual migration is currently running.</summary>
    public bool IsMigrating
    {
        get => _isMigrating;
        private set => this.RaiseAndSetIfChanged(ref _isMigrating, value);
    }

    /// <summary>
    /// Populates the page from the current settings and profile library.
    /// Call after settings have loaded, the profile library has been scanned, and the
    /// startup migration pass has run.
    /// </summary>
    public void LoadFromSettings()
    {
        _loading = true;
        try
        {
            EnableAutoLoading = _settingsService.Settings.EnableAutoLoading;
            RebuildAvailableProfiles();
            RebuildRows();
        }
        finally
        {
            _loading = false;
        }

        _ = RefreshLegacyDetectionAsync();
    }

    /// <summary>
    /// Refreshes the profile dropdowns when the profile library changes (a profile was
    /// added, renamed, or deleted). Marshalled to the UI thread; suppresses save side effects.
    /// </summary>
    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _loading = true;
            try
            {
                RebuildAvailableProfiles();
                foreach (var row in Triggers)
                {
                    row.ResolveProfile(AvailableProfiles);
                }
            }
            finally
            {
                _loading = false;
            }

            _ = RefreshLegacyDetectionAsync();
        });
    }

    private void RebuildAvailableProfiles()
    {
        AvailableProfiles.Clear();
        foreach (var entry in _profileLibrary.Entries)
        {
            AvailableProfiles.Add(entry);
        }
    }

    private void RebuildRows()
    {
        foreach (var sub in _rowSubscriptions.Values)
        {
            sub.Dispose();
        }
        _rowSubscriptions.Clear();
        Triggers.Clear();

        foreach (var trigger in _settingsService.Settings.AutoLoadTriggers)
        {
            AddRow(trigger);
        }
    }

    private void AddRow(AutoLoadTrigger model)
    {
        var row = new ProcessTriggerViewModel(model, AvailableProfiles, _processPicker, _filePicker);
        Triggers.Add(row);
        _rowSubscriptions[row] = row.Changed.Subscribe(_ => ScheduleSave());
    }

    private void AddTrigger()
    {
        AddRow(new AutoLoadTrigger());
        ScheduleSave();
    }

    private void RemoveTrigger(ProcessTriggerViewModel row)
    {
        if (_rowSubscriptions.Remove(row, out var sub))
        {
            sub.Dispose();
        }
        Triggers.Remove(row);
        ScheduleSave();
    }

    private void MoveUp(ProcessTriggerViewModel row)
    {
        var idx = Triggers.IndexOf(row);
        if (idx <= 0)
        {
            return;
        }
        Triggers.Move(idx, idx - 1);
        ScheduleSave();
    }

    private void MoveDown(ProcessTriggerViewModel row)
    {
        var idx = Triggers.IndexOf(row);
        if (idx < 0 || idx >= Triggers.Count - 1)
        {
            return;
        }
        Triggers.Move(idx, idx + 1);
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        if (!_loading)
        {
            _isDirty = true;
            _saveTrigger.OnNext(Unit.Default);
        }
    }

    private async Task SaveAsync()
    {
        if (!_isDirty)
        {
            await SaveSettingsAsync();
            return;
        }

        try
        {
            // Snapshot every row into FRESH trigger instances, then REPLACE the settings
            // list. The process monitor enumerates the published list (and its elements)
            // from a non-UI thread, so neither the list nor any trigger instance it
            // contains may ever be mutated in place.
            var snapshot = Triggers.Select(row => row.ToTrigger()).ToList();

            _settingsService.Settings.AutoLoadTriggers = snapshot;
            _settingsService.Settings.EnableAutoLoading = EnableAutoLoading;
            await _settingsService.SaveAsync();
            _isDirty = false;
            _logger.LogInformation("Saved {Count} global auto-load trigger(s)", snapshot.Count);
        }
        catch (Exception ex)
        {
            // _isDirty stays true; the next change re-enters the save queue.
            _logger.LogError(ex, "Failed to save auto-load triggers — will retry on next change");
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            if (_settingsService.Settings.EnableAutoLoading == EnableAutoLoading)
            {
                return;
            }

            _settingsService.Settings.EnableAutoLoading = EnableAutoLoading;
            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save auto-load enable flag");
        }
    }

    private async Task RefreshLegacyDetectionAsync()
    {
        try
        {
            // DetectAsync reads every profile file; keep that I/O off the UI thread —
            // this runs on every LibraryChanged (profile add/rename/delete).
            var legacy = await Task.Run(() => _migrator.DetectAsync());
            Dispatcher.UIThread.Post(() => LegacyProfileCount = legacy.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Legacy auto-load trigger detection failed");
        }
    }

    private async Task MigrateLegacyAsync()
    {
        IsMigrating = true;
        try
        {
            // Whole-library file I/O — run off the UI thread to keep the page responsive.
            var result = await Task.Run(() => _migrator.MigrateAsync());

            MigrationStatusMessage = result.Failures.Count == 0
                ? $"Imported {result.TriggerCount} trigger{(result.TriggerCount == 1 ? string.Empty : "s")} " +
                  $"from {result.MigratedProfileCount} profile{(result.MigratedProfileCount == 1 ? string.Empty : "s")}."
                : $"Imported {result.TriggerCount} trigger(s); {result.Failures.Count} file(s) failed: " +
                  string.Join("; ", result.Failures.Select(f => $"{f.ProfilePath} — {f.Reason}"));

            // Reload rows from the (now grown) global list without re-triggering a save.
            _loading = true;
            try
            {
                RebuildRows();
            }
            finally
            {
                _loading = false;
            }

            LegacyProfileCount = (await Task.Run(() => _migrator.DetectAsync())).Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual auto-load trigger migration failed");
            MigrationStatusMessage = $"Migration failed: {ex.Message}";
        }
        finally
        {
            IsMigrating = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _profileLibrary.LibraryChanged -= OnLibraryChanged;
        foreach (var sub in _rowSubscriptions.Values)
        {
            sub.Dispose();
        }
        _rowSubscriptions.Clear();
        _subscriptions.Dispose();
        _saveTrigger.Dispose();
    }
}
