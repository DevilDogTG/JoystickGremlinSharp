// SPDX-License-Identifier: GPL-3.0-only

using System.Reactive;
using JoystickGremlin.Core.Configuration;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Wraps a <see cref="ProcessProfileMapping"/> domain model for display in the Settings page.
/// </summary>
public sealed class ProcessMappingViewModel : ReactiveObject
{
    private string _executablePath;
    private string _profilePath;
    private bool _isEnabled;
    private bool _autoStart;
    private bool _remainActiveOnFocusLoss;

    /// <summary>Gets the underlying domain model, updated by <see cref="ApplyToModel"/>.</summary>
    public ProcessProfileMapping Model { get; }

    /// <summary>Gets or sets the executable path or regex pattern.</summary>
    public string ExecutablePath
    {
        get => _executablePath;
        set => this.RaiseAndSetIfChanged(ref _executablePath, value);
    }

    /// <summary>Gets or sets the profile file path.</summary>
    public string ProfilePath
    {
        get => _profilePath;
        set => this.RaiseAndSetIfChanged(ref _profilePath, value);
    }

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

    /// <summary>Gets the command that opens a file picker to browse for a profile.</summary>
    public ReactiveCommand<Unit, Unit> BrowseProfileCommand { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessMappingViewModel"/>.
    /// </summary>
    public ProcessMappingViewModel(ProcessProfileMapping model, IFilePickerService filePicker)
    {
        Model = model;

        _executablePath          = model.ExecutablePath;
        _profilePath             = model.ProfilePath;
        _isEnabled               = model.IsEnabled;
        _autoStart               = model.AutoStart;
        _remainActiveOnFocusLoss = model.RemainActiveOnFocusLoss;

        BrowseProfileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await filePicker.PickOpenFileAsync(
                "Select Profile", "Joystick Gremlin Profile", "*.json");
            if (path is not null)
                ProfilePath = path;
        });
    }

    /// <summary>
    /// Writes the current ViewModel values back to the underlying domain model.
    /// Call before saving settings.
    /// </summary>
    public void ApplyToModel()
    {
        Model.ExecutablePath          = ExecutablePath;
        Model.ProfilePath             = ProfilePath;
        Model.IsEnabled               = IsEnabled;
        Model.AutoStart               = AutoStart;
        Model.RemainActiveOnFocusLoss = RemainActiveOnFocusLoss;
    }
}
