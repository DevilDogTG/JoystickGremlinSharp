// SPDX-License-Identifier: GPL-3.0-only

using System.IO;
using System.Reactive;
using JoystickGremlin.App.Services;
using JoystickGremlin.Core.Profile;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Wraps a <see cref="ProcessTrigger"/> for display in a profile's auto-load triggers list.
/// The executable is chosen either via the process picker (name match) or by browsing for an
/// .exe file (path match). The owning profile is identified by the parent group.
/// </summary>
public sealed class ProcessTriggerViewModel : ReactiveObject
{
    private readonly IProcessPickerDialogService _processPicker;
    private readonly IFilePickerService _filePicker;

    private ProcessMatchType _matchType;
    private string _executableName;
    private string _executablePath;
    private bool _isEnabled;
    private bool _autoStart;
    private bool _remainActiveOnFocusLoss;

    /// <summary>Gets the underlying domain model, updated by <see cref="ApplyToModel"/>.</summary>
    public ProcessTrigger Model { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessTriggerViewModel"/>.
    /// </summary>
    /// <param name="model">The trigger domain model.</param>
    /// <param name="processPicker">Service used to pick a running process.</param>
    /// <param name="filePicker">Service used to browse for an executable file.</param>
    public ProcessTriggerViewModel(
        ProcessTrigger model,
        IProcessPickerDialogService processPicker,
        IFilePickerService filePicker)
    {
        Model = model;
        _processPicker = processPicker;
        _filePicker = filePicker;

        _matchType               = model.MatchType;
        _executableName          = model.ExecutableName;
        _executablePath          = model.ExecutablePath;
        _isEnabled               = model.IsEnabled;
        _autoStart               = model.AutoStart;
        _remainActiveOnFocusLoss = model.RemainActiveOnFocusLoss;

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
    /// Writes the current ViewModel values back to the underlying domain model.
    /// Call before saving the owning profile.
    /// </summary>
    public void ApplyToModel()
    {
        Model.MatchType               = MatchType;
        Model.ExecutableName          = ExecutableName;
        Model.ExecutablePath          = ExecutablePath;
        Model.IsEnabled               = IsEnabled;
        Model.AutoStart               = AutoStart;
        Model.RemainActiveOnFocusLoss = RemainActiveOnFocusLoss;
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
