// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using JoystickGremlin.App.Services;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Profile;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Wraps a <see cref="ProcessProfileMapping"/> domain model for display on the Auto-load page.
/// The executable is chosen either via the process picker (name match) or by browsing for an
/// .exe file (path match); the profile is chosen from the application's profile library.
/// </summary>
public sealed class ProcessMappingViewModel : ReactiveObject
{
    private readonly IProcessPickerDialogService _processPicker;
    private readonly IFilePickerService _filePicker;
    private readonly ObservableCollection<ProfileEntry> _availableProfiles;

    private ProcessMatchType _matchType;
    private string _executableName;
    private string _executablePath;
    private string _profilePath;
    private bool _isEnabled;
    private bool _autoStart;
    private bool _remainActiveOnFocusLoss;
    private ProfileEntry? _selectedProfile;
    private bool _suppressProfileSync;

    /// <summary>Gets the underlying domain model, updated by <see cref="ApplyToModel"/>.</summary>
    public ProcessProfileMapping Model { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessMappingViewModel"/>.
    /// </summary>
    /// <param name="model">The mapping domain model.</param>
    /// <param name="processPicker">Service used to pick a running process.</param>
    /// <param name="filePicker">Service used to browse for an executable file.</param>
    /// <param name="availableProfiles">The shared collection of selectable profiles (owned by the page).</param>
    public ProcessMappingViewModel(
        ProcessProfileMapping model,
        IProcessPickerDialogService processPicker,
        IFilePickerService filePicker,
        ObservableCollection<ProfileEntry> availableProfiles)
    {
        Model = model;
        _processPicker = processPicker;
        _filePicker = filePicker;
        _availableProfiles = availableProfiles;

        _matchType               = model.MatchType;
        _executableName          = model.ExecutableName;
        _executablePath          = model.ExecutablePath;
        _profilePath             = model.ProfilePath;
        _isEnabled               = model.IsEnabled;
        _autoStart               = model.AutoStart;
        _remainActiveOnFocusLoss = model.RemainActiveOnFocusLoss;

        RefreshSelectedProfile();

        PickProcessCommand = ReactiveCommand.CreateFromTask(PickProcessAsync);
        BrowseExecutableCommand = ReactiveCommand.CreateFromTask(BrowseExecutableAsync);
    }

    /// <summary>Gets how the foreground executable is matched (by name or by full path).</summary>
    public ProcessMatchType MatchType
    {
        get => _matchType;
        set
        {
            this.RaiseAndSetIfChanged(ref _matchType, value);
            RaiseMatchDisplayChanged();
        }
    }

    /// <summary>Gets or sets the executable file name matched in name mode (e.g. <c>DCS.exe</c>).</summary>
    public string ExecutableName
    {
        get => _executableName;
        set
        {
            this.RaiseAndSetIfChanged(ref _executableName, value);
            RaiseMatchDisplayChanged();
        }
    }

    /// <summary>Gets or sets the full executable path (matched in path mode; shown for info in name mode).</summary>
    public string ExecutablePath
    {
        get => _executablePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _executablePath, value);
            RaiseMatchDisplayChanged();
        }
    }

    /// <summary>Gets or sets the profile file path.</summary>
    public string ProfilePath
    {
        get => _profilePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _profilePath, value);
            this.RaisePropertyChanged(nameof(IsProfileMissing));
        }
    }

    /// <summary>Gets or sets the selected profile entry. Setting it updates <see cref="ProfilePath"/>.</summary>
    public ProfileEntry? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfile, value);
            if (!_suppressProfileSync && value is not null)
                ProfilePath = value.FilePath;
            this.RaisePropertyChanged(nameof(IsProfileMissing));
        }
    }

    /// <summary>
    /// Gets a value indicating whether a profile is configured but no longer exists in the library
    /// (e.g. it was renamed or deleted).
    /// </summary>
    public bool IsProfileMissing => !string.IsNullOrEmpty(ProfilePath) && SelectedProfile is null;

    /// <summary>Gets or sets a value indicating whether this mapping is active.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    /// <summary>Gets or sets whether to auto-start the pipeline when this mapping activates.</summary>
    public bool AutoStart
    {
        get => _autoStart;
        set => this.RaiseAndSetIfChanged(ref _autoStart, value);
    }

    /// <summary>Gets or sets whether to keep the pipeline active when the mapped app loses focus.</summary>
    public bool RemainActiveOnFocusLoss
    {
        get => _remainActiveOnFocusLoss;
        set => this.RaiseAndSetIfChanged(ref _remainActiveOnFocusLoss, value);
    }

    /// <summary>Gets the text shown in the match column: exe name (name mode) or full path (path mode).</summary>
    public string MatchDisplay => MatchType == ProcessMatchType.ExecutableName
        ? (string.IsNullOrEmpty(ExecutableName) ? "(no process selected)" : ExecutableName)
        : (string.IsNullOrEmpty(ExecutablePath) ? "(no path set)" : ExecutablePath);

    /// <summary>Gets the tooltip for the match column (the full captured path, when available).</summary>
    public string MatchTooltip => string.IsNullOrEmpty(ExecutablePath) ? MatchDisplay : ExecutablePath;

    /// <summary>Gets a short badge describing the current match mode.</summary>
    public string MatchModeLabel => MatchType == ProcessMatchType.ExecutableName ? "name" : "path";

    /// <summary>Gets the command that opens the process picker (sets name-match mode).</summary>
    public ReactiveCommand<Unit, Unit> PickProcessCommand { get; }

    /// <summary>Gets the command that browses for an executable file (sets path-match mode).</summary>
    public ReactiveCommand<Unit, Unit> BrowseExecutableCommand { get; }

    /// <summary>
    /// Re-resolves <see cref="SelectedProfile"/> from <see cref="ProfilePath"/> against the current
    /// profile library, without rewriting <see cref="ProfilePath"/> (so a missing profile is preserved).
    /// </summary>
    public void RefreshSelectedProfile()
    {
        _suppressProfileSync = true;
        SelectedProfile = _availableProfiles.FirstOrDefault(e =>
            string.Equals(e.FilePath, ProfilePath, StringComparison.OrdinalIgnoreCase));
        _suppressProfileSync = false;
    }

    /// <summary>
    /// Writes the current ViewModel values back to the underlying domain model.
    /// Call before saving settings.
    /// </summary>
    public void ApplyToModel()
    {
        Model.MatchType               = MatchType;
        Model.ExecutableName          = ExecutableName;
        Model.ExecutablePath          = ExecutablePath;
        Model.ProfilePath             = ProfilePath;
        Model.IsEnabled               = IsEnabled;
        Model.AutoStart               = AutoStart;
        Model.RemainActiveOnFocusLoss = RemainActiveOnFocusLoss;
    }

    private async System.Threading.Tasks.Task PickProcessAsync()
    {
        var picked = await _processPicker.PickProcessAsync();
        if (picked is null) return;

        MatchType      = ProcessMatchType.ExecutableName;
        ExecutableName = picked.ExecutableName;
        ExecutablePath = picked.ExecutablePath;
    }

    private async System.Threading.Tasks.Task BrowseExecutableAsync()
    {
        var path = await _filePicker.PickOpenFileAsync("Select Executable", "Executable", "*.exe");
        if (path is null) return;

        MatchType      = ProcessMatchType.ExecutablePath;
        ExecutablePath = path.Replace('\\', '/');
        ExecutableName = Path.GetFileName(path);
    }

    private void RaiseMatchDisplayChanged()
    {
        this.RaisePropertyChanged(nameof(MatchDisplay));
        this.RaisePropertyChanged(nameof(MatchTooltip));
        this.RaisePropertyChanged(nameof(MatchModeLabel));
    }
}
