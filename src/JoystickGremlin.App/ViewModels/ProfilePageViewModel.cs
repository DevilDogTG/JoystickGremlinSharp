// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Profile page — manages modes (add/remove/rename/parent)
/// and shows the current profile's input-binding hierarchy.
/// </summary>
public sealed class ProfilePageViewModel : ViewModelBase, IDisposable
{
    private const string RootModeOption = "(root mode)";

    private readonly IProfileState _profileState;
    private readonly ILogger<ProfilePageViewModel> _logger;
    private ModeViewModel? _selectedMode;
    private string _editName = string.Empty;
    private string _editParentName = RootModeOption;

    /// <summary>
    /// Initializes a new instance of <see cref="ProfilePageViewModel"/>.
    /// </summary>
    public ProfilePageViewModel(IProfileState profileState, ILogger<ProfilePageViewModel> logger)
    {
        _profileState = profileState;
        _logger = logger;

        Modes = new ObservableCollection<ModeViewModel>();
        AvailableParentNames = new ObservableCollection<string>();

        var hasProfile = this.WhenAnyValue(x => x.HasProfile);
        var hasSelection = this.WhenAnyValue(x => x.SelectedMode).Select(m => m is not null);

        AddModeCommand = ReactiveCommand.Create(AddMode, hasProfile);
        RemoveModeCommand = ReactiveCommand.Create(RemoveMode, hasSelection);
        CommitEditCommand = ReactiveCommand.Create(CommitEdit, hasSelection);

        _ = this.WhenAnyValue(x => x.SelectedMode)
            .Subscribe(OnSelectedModeChanged);

        _profileState.ProfileChanged += OnProfileChanged;
    }

    /// <summary>Gets the list of mode ViewModels in the current profile.</summary>
    public ObservableCollection<ModeViewModel> Modes { get; }

    /// <summary>Gets the available parent mode options (includes "(root mode)" sentinel).</summary>
    public ObservableCollection<string> AvailableParentNames { get; }

    /// <summary>Gets or sets the selected mode in the list.</summary>
    public ModeViewModel? SelectedMode
    {
        get => _selectedMode;
        set => this.RaiseAndSetIfChanged(ref _selectedMode, value);
    }

    /// <summary>Gets or sets the name text in the edit form.</summary>
    public string EditName
    {
        get => _editName;
        set => this.RaiseAndSetIfChanged(ref _editName, value);
    }

    /// <summary>Gets or sets the parent name selection in the edit form.</summary>
    public string EditParentName
    {
        get => _editParentName;
        set => this.RaiseAndSetIfChanged(ref _editParentName, value);
    }

    /// <summary>Gets a value indicating whether a profile is currently loaded.</summary>
    public bool HasProfile => _profileState.CurrentProfile is not null;

    /// <summary>Gets the command that adds a new mode to the profile.</summary>
    public ReactiveCommand<Unit, Unit> AddModeCommand { get; }

    /// <summary>Gets the command that removes the selected mode.</summary>
    public ReactiveCommand<Unit, Unit> RemoveModeCommand { get; }

    /// <summary>Gets the command that commits the edit-form values to the selected mode.</summary>
    public ReactiveCommand<Unit, Unit> CommitEditCommand { get; }

    private void OnProfileChanged(object? sender, JoystickGremlin.Core.Profile.Profile? profile)
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.RaisePropertyChanged(nameof(HasProfile));
            RebuildModes(profile);
        });
    }

    private void RebuildModes(JoystickGremlin.Core.Profile.Profile? profile)
    {
        Modes.Clear();
        if (profile is null) return;
        foreach (var mode in profile.Modes)
            Modes.Add(new ModeViewModel(mode));
        SelectedMode = Modes.Count > 0 ? Modes[0] : null;
    }

    private void OnSelectedModeChanged(ModeViewModel? mode)
    {
        if (mode is null)
        {
            EditName = string.Empty;
            EditParentName = RootModeOption;
        }
        else
        {
            EditName = mode.Name;
            EditParentName = mode.ParentModeName ?? RootModeOption;
        }
        RebuildParentOptions();
    }

    private void RebuildParentOptions()
    {
        AvailableParentNames.Clear();
        AvailableParentNames.Add(RootModeOption);
        foreach (var m in Modes.Where(m => m != SelectedMode))
            AvailableParentNames.Add(m.Name);
    }

    private void AddMode()
    {
        var profile = _profileState.CurrentProfile;
        if (profile is null) return;
        var newMode = new Mode { Name = "New Mode" };
        profile.Modes.Add(newMode);
        var vm = new ModeViewModel(newMode);
        Modes.Add(vm);
        SelectedMode = vm;
        _profileState.NotifyProfileModified();
    }

    private void RemoveMode()
    {
        if (SelectedMode is null) return;
        var profile = _profileState.CurrentProfile;
        if (profile is null) return;
        profile.Modes.Remove(SelectedMode.Model);
        Modes.Remove(SelectedMode);
        SelectedMode = Modes.Count > 0 ? Modes[0] : null;
        _profileState.NotifyProfileModified();
    }

    private void CommitEdit()
    {
        if (SelectedMode is null) return;
        SelectedMode.Name = EditName;
        SelectedMode.ParentModeName = EditParentName == RootModeOption ? null : EditParentName;
        SelectedMode.CommitToModel();
        _profileState.NotifyProfileModified();
        RebuildParentOptions();
    }

    /// <inheritdoc/>
    public void Dispose() => _profileState.ProfileChanged -= OnProfileChanged;
}
