// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using JoystickGremlin.App.Services;
using JoystickGremlin.Core.Profile;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Groups all <see cref="ProcessTriggerViewModel"/> rows that belong to a single profile.
/// Owns the per-profile add / remove / reorder commands and tracks whether the group has
/// pending unsaved changes so the page can save only the profiles that actually changed.
/// </summary>
public sealed class ProfileTriggersGroupViewModel : ReactiveObject
{
    private readonly IProcessPickerDialogService _processPicker;
    private readonly IFilePickerService _filePicker;
    private readonly Action _onChanged;

    private bool _isDirty;
    private bool _isExpanded;

    /// <summary>Gets the profile whose triggers are displayed in this group.</summary>
    public ProfileEntry Profile { get; }

    /// <summary>Gets the trigger rows for this profile, in evaluation order.</summary>
    public ObservableCollection<ProcessTriggerViewModel> Triggers { get; } = [];

    /// <summary>Gets the command that adds a new blank trigger to this profile.</summary>
    public ReactiveCommand<Unit, Unit> AddTriggerCommand { get; }

    /// <summary>Gets the command that removes a trigger from this profile.</summary>
    public ReactiveCommand<ProcessTriggerViewModel, Unit> RemoveTriggerCommand { get; }

    /// <summary>Gets the command that moves the given trigger up (higher priority).</summary>
    public ReactiveCommand<ProcessTriggerViewModel, Unit> MoveUpCommand { get; }

    /// <summary>Gets the command that moves the given trigger down (lower priority).</summary>
    public ReactiveCommand<ProcessTriggerViewModel, Unit> MoveDownCommand { get; }

    /// <summary>Gets a value indicating whether this group has unsaved changes.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    /// <summary>Gets or sets whether the group's triggers list is expanded in the UI.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    /// <summary>Gets the display label shown in the group header.</summary>
    public string DisplayLabel => Profile.DisplayLabel;

    /// <summary>Gets a one-line summary used in the collapsed header.</summary>
    public string TriggerCountSummary =>
        Triggers.Count == 0 ? "no triggers" : $"{Triggers.Count} trigger{(Triggers.Count == 1 ? string.Empty : "s")}";

    /// <summary>
    /// Initializes a new instance of <see cref="ProfileTriggersGroupViewModel"/>.
    /// </summary>
    /// <param name="profile">The owning profile entry.</param>
    /// <param name="initialTriggers">
    /// The current snapshot of triggers from <see cref="ProfileEntry.AutoLoadTriggers"/>.
    /// Each one is copied into a new <see cref="ProcessTrigger"/> model so edits do not mutate
    /// the library snapshot before the debounced save runs.
    /// </param>
    /// <param name="processPicker">Process picker service.</param>
    /// <param name="filePicker">File picker service.</param>
    /// <param name="onChanged">Callback invoked whenever this group becomes dirty.</param>
    public ProfileTriggersGroupViewModel(
        ProfileEntry profile,
        IReadOnlyList<ProcessTrigger> initialTriggers,
        IProcessPickerDialogService processPicker,
        IFilePickerService filePicker,
        Action onChanged)
    {
        Profile = profile;
        _processPicker = processPicker;
        _filePicker = filePicker;
        _onChanged = onChanged;
        _isExpanded = initialTriggers.Count > 0;

        foreach (var src in initialTriggers)
        {
            AddRow(CloneTrigger(src), silent: true);
        }

        AddTriggerCommand    = ReactiveCommand.Create(AddTrigger);
        RemoveTriggerCommand = ReactiveCommand.Create<ProcessTriggerViewModel>(RemoveTrigger);
        MoveUpCommand        = ReactiveCommand.Create<ProcessTriggerViewModel>(MoveUp);
        MoveDownCommand      = ReactiveCommand.Create<ProcessTriggerViewModel>(MoveDown);
    }

    /// <summary>
    /// Builds the persisted <see cref="ProcessTrigger"/> list from the current row VMs and
    /// clears the dirty flag.
    /// </summary>
    public List<ProcessTrigger> BuildModelList()
    {
        var list = new List<ProcessTrigger>(Triggers.Count);
        foreach (var row in Triggers)
        {
            row.ApplyToModel();
            list.Add(row.Model);
        }
        IsDirty = false;
        return list;
    }

    private void AddTrigger()
    {
        AddRow(new ProcessTrigger(), silent: false);
        IsExpanded = true;
    }

    private void RemoveTrigger(ProcessTriggerViewModel row)
    {
        Triggers.Remove(row);
        this.RaisePropertyChanged(nameof(TriggerCountSummary));
        MarkDirty();
    }

    private void MoveUp(ProcessTriggerViewModel row)
    {
        var idx = Triggers.IndexOf(row);
        if (idx <= 0) return;
        Triggers.Move(idx, idx - 1);
        MarkDirty();
    }

    private void MoveDown(ProcessTriggerViewModel row)
    {
        var idx = Triggers.IndexOf(row);
        if (idx < 0 || idx >= Triggers.Count - 1) return;
        Triggers.Move(idx, idx + 1);
        MarkDirty();
    }

    private void AddRow(ProcessTrigger model, bool silent)
    {
        var vm = new ProcessTriggerViewModel(model, _processPicker, _filePicker);
        Triggers.Add(vm);
        vm.Changed.Subscribe(_ => MarkDirty());
        if (!silent)
        {
            this.RaisePropertyChanged(nameof(TriggerCountSummary));
            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        IsDirty = true;
        _onChanged();
    }

    private static ProcessTrigger CloneTrigger(ProcessTrigger src) => new()
    {
        MatchType               = src.MatchType,
        ExecutableName          = src.ExecutableName,
        ExecutablePath          = src.ExecutablePath,
        IsEnabled               = src.IsEnabled,
        AutoStart               = src.AutoStart,
        RemainActiveOnFocusLoss = src.RemainActiveOnFocusLoss,
    };
}
