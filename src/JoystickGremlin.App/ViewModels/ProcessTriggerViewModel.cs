// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using Avalonia.Threading;
using JoystickGremlin.App.Services;
using JoystickGremlin.Core.ProcessMonitor;
using JoystickGremlin.Core.Profile;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Wraps an <see cref="AutoLoadTrigger"/> for display as one row of the global auto-load
/// trigger list. The target profile is chosen from the library dropdown; the executable is
/// chosen either via the process picker (name match) or by browsing for an .exe file
/// (path match).
/// </summary>
public sealed class ProcessTriggerViewModel : ReactiveObject
{
    private readonly IProcessPickerDialogService _processPicker;
    private readonly IFilePickerService _filePicker;

    private ProfileEntry? _selectedProfile;
    private string _profilePath;
    private ProcessMatchType _matchType;
    private string _executableName;
    private string _executablePath;
    private bool _isEnabled;
    private bool _autoStart;
    private bool _remainActiveOnFocusLoss;

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessTriggerViewModel"/>.
    /// </summary>
    /// <param name="model">
    /// The trigger to copy the initial row values from. The instance is NOT retained —
    /// it may be referenced by the live settings list, which the process monitor reads
    /// from a non-UI thread, so the row must never write through to it. Snapshots for
    /// persistence are produced by <see cref="ToTrigger"/>.
    /// </param>
    /// <param name="availableProfiles">
    /// The shared library entry collection the profile dropdown binds to. Owned by the page;
    /// the row resolves its <see cref="SelectedProfile"/> from it.
    /// </param>
    /// <param name="processPicker">Service used to pick a running process.</param>
    /// <param name="filePicker">Service used to browse for an executable file.</param>
    public ProcessTriggerViewModel(
        AutoLoadTrigger model,
        ObservableCollection<ProfileEntry> availableProfiles,
        IProcessPickerDialogService processPicker,
        IFilePickerService filePicker)
    {
        AvailableProfiles = availableProfiles;
        _processPicker = processPicker;
        _filePicker = filePicker;

        _profilePath             = model.ProfilePath;
        _selectedProfile         = availableProfiles.FirstOrDefault(e =>
            string.Equals(e.FilePath, model.ProfilePath, StringComparison.OrdinalIgnoreCase));
        _matchType               = model.MatchType;
        _executableName          = model.ExecutableName;
        _executablePath          = model.ExecutablePath;
        _isEnabled               = model.IsEnabled;
        _autoStart               = model.AutoStart;
        _remainActiveOnFocusLoss = model.RemainActiveOnFocusLoss;

        PickProcessCommand = ReactiveCommand.CreateFromTask(PickProcessAsync);
        BrowseExecutableCommand = ReactiveCommand.CreateFromTask(BrowseExecutableAsync);
    }

    /// <summary>Gets the shared library entry collection shown in the profile dropdown.</summary>
    public ObservableCollection<ProfileEntry> AvailableProfiles { get; }

    /// <summary>
    /// Gets or sets the library entry of the profile this trigger loads.
    /// <c>null</c> when no profile has been picked yet, or when the stored
    /// <see cref="ProfilePath"/> points at a profile that left the library.
    /// </summary>
    public ProfileEntry? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            // The ComboBox pushes null through the two-way binding while the view is
            // re-attaching (SelectedItem can bind before ItemsSource has items) or while
            // the shared items collection is being rebuilt. The UI offers no way to
            // genuinely clear a trigger's profile — treat the null as transient: keep the
            // current value and re-announce it so the control resyncs once items exist.
            if (value is null && !string.IsNullOrEmpty(_profilePath))
            {
                Dispatcher.UIThread.Post(() => this.RaisePropertyChanged(nameof(SelectedProfile)));
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedProfile, value);
            if (value is not null)
            {
                ProfilePath = value.FilePath;
            }
            this.RaisePropertyChanged(nameof(IsProfileUnresolved));
            this.RaisePropertyChanged(nameof(ProfileTooltip));
        }
    }

    /// <summary>
    /// Gets or sets the absolute path of the profile to load. Kept even when the profile
    /// is missing from the library so the reference survives until the user re-points it.
    /// </summary>
    public string ProfilePath
    {
        get => _profilePath;
        private set => this.RaiseAndSetIfChanged(ref _profilePath, value);
    }

    /// <summary>Gets a value indicating whether the stored profile path resolves to no library entry.</summary>
    public bool IsProfileUnresolved => _selectedProfile is null && !string.IsNullOrEmpty(_profilePath);

    /// <summary>Gets the tooltip for the profile column (the stored profile path, when available).</summary>
    public string ProfileTooltip => string.IsNullOrEmpty(_profilePath)
        ? "Select the profile to load when this trigger activates"
        : _profilePath;

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

    /// <summary>Gets or sets a value indicating whether this trigger is active.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    /// <summary>Gets or sets whether to auto-start the pipeline when this trigger activates.</summary>
    public bool AutoStart
    {
        get => _autoStart;
        set => this.RaiseAndSetIfChanged(ref _autoStart, value);
    }

    /// <summary>Gets or sets whether to keep the pipeline active when the triggered app loses focus.</summary>
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
    /// Produces a fresh <see cref="AutoLoadTrigger"/> snapshot of the current row state.
    /// Always a new instance: trigger objects already published to the settings list are
    /// enumerated by the process monitor on a non-UI thread and must never be mutated.
    /// </summary>
    /// <returns>A new trigger carrying the row's current values.</returns>
    public AutoLoadTrigger ToTrigger() => new()
    {
        ProfilePath             = ProfilePath,
        MatchType               = MatchType,
        ExecutableName          = ExecutableName,
        ExecutablePath          = ExecutablePath,
        IsEnabled               = IsEnabled,
        AutoStart               = AutoStart,
        RemainActiveOnFocusLoss = RemainActiveOnFocusLoss,
    };

    /// <summary>
    /// Re-resolves <see cref="SelectedProfile"/> against a fresh library entry list after
    /// the library changed, without touching the stored <see cref="ProfilePath"/>.
    /// </summary>
    public void ResolveProfile(IReadOnlyList<ProfileEntry> entries)
    {
        var resolved = entries.FirstOrDefault(e =>
            string.Equals(e.FilePath, _profilePath, StringComparison.OrdinalIgnoreCase));
        this.RaiseAndSetIfChanged(ref _selectedProfile, resolved, nameof(SelectedProfile));
        this.RaisePropertyChanged(nameof(IsProfileUnresolved));
        this.RaisePropertyChanged(nameof(ProfileTooltip));
    }

    private async System.Threading.Tasks.Task PickProcessAsync()
    {
        var picked = await _processPicker.PickProcessAsync();
        if (picked is null)
            return;

        MatchType      = ProcessMatchType.ExecutableName;
        ExecutableName = picked.ExecutableName;
        ExecutablePath = picked.ExecutablePath;
    }

    private async System.Threading.Tasks.Task BrowseExecutableAsync()
    {
        var path = await _filePicker.PickOpenFileAsync("Select Executable", "Executable", "*.exe");
        if (path is null)
            return;

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
