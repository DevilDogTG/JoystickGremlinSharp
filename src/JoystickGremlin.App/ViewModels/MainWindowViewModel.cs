// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.App.ViewModels.InputViewer;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Modes;
using JoystickGremlin.Core.Pipeline;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Velopack;
using Velopack.Sources;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the main application window. Owns the top toolbar state
/// (profile path, active mode, start/stop) and drives sidebar navigation.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IEventPipeline _eventPipeline;
    private readonly IModeManager _modeManager;
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileState _profileState;
    private readonly ISettingsService _settingsService;
    private readonly IFilePickerService _filePicker;
    private readonly IDeviceManager _deviceManager;
    private readonly DevicesPageViewModel _devicesPage;
    private readonly BindingsPageViewModel _bindingsPage;
    private readonly SettingsPageViewModel _settingsPage;
    private readonly InputViewerPageViewModel _inputViewerPage;
    private readonly ILogger<MainWindowViewModel> _logger;

    private readonly ObservableAsPropertyHelper<string> _toggleButtonLabel;
    private ViewModelBase _currentPage;
    private NavItemViewModel? _selectedNavItem;
    private bool _isGremlinActive;
    private string _selectedModeName = string.Empty;
    private string _profilePath = "(no profile)";
    private bool _hasProfile;
    private bool _suppressModeUpdate;

    /// <summary>
    /// Initializes a new instance of <see cref="MainWindowViewModel"/>.
    /// </summary>
    public MainWindowViewModel(
        DevicesPageViewModel devicesPage,
        ProfilePageViewModel profilePage,
        BindingsPageViewModel bindingsPage,
        SettingsPageViewModel settingsPage,
        InputViewerPageViewModel inputViewerPage,
        IEventPipeline eventPipeline,
        IModeManager modeManager,
        IProfileRepository profileRepository,
        IProfileState profileState,
        ISettingsService settingsService,
        IFilePickerService filePicker,
        IDeviceManager deviceManager,
        ILogger<MainWindowViewModel> logger)
    {
        _eventPipeline = eventPipeline;
        _modeManager = modeManager;
        _profileRepository = profileRepository;
        _profileState = profileState;
        _settingsService = settingsService;
        _filePicker = filePicker;
        _deviceManager = deviceManager;
        _devicesPage = devicesPage;
        _bindingsPage = bindingsPage;
        _settingsPage = settingsPage;
        _inputViewerPage = inputViewerPage;
        _logger = logger;

        var navItems = new[]
        {
            new NavItemViewModel { Title = "Devices",      Icon = "🎮", Page = devicesPage      },
            new NavItemViewModel { Title = "Input Viewer", Icon = "👁", Page = inputViewerPage  },
            new NavItemViewModel { Title = "Bindings",     Icon = "🔗", Page = bindingsPage     },
            new NavItemViewModel { Title = "Profile",      Icon = "📋", Page = profilePage      },
            new NavItemViewModel { Title = "Settings",     Icon = "⚙️", Page = settingsPage     },
        };

        NavItems = new ObservableCollection<NavItemViewModel>(navItems);
        AvailableModes = new ObservableCollection<string>();
        _currentPage = devicesPage;
        _selectedNavItem = navItems[0];

        _toggleButtonLabel = this.WhenAnyValue(x => x.IsGremlinActive)
            .Select(active => active ? "⏹  Stop" : "▶  Start")
            .ToProperty(this, x => x.ToggleButtonLabel, initialValue: "▶  Start");

        _ = this.WhenAnyValue(x => x.SelectedNavItem)
            .WhereNotNull()
            .Subscribe(item => CurrentPage = item.Page);

        ToggleActiveCommand      = ReactiveCommand.CreateFromTask(ToggleActiveAsync);
        OpenProfileCommand       = ReactiveCommand.CreateFromTask(OpenProfileAsync);
        NewProfileCommand        = ReactiveCommand.Create(NewProfile);
        CheckForUpdatesCommand   = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync);

        _profileState.ProfileChanged += OnProfileChanged;
        _modeManager.ModeChanged += OnModeChanged;
    }

    /// <summary>Gets the sidebar navigation items.</summary>
    public ObservableCollection<NavItemViewModel> NavItems { get; }

    /// <summary>Gets the list of mode names for the current profile.</summary>
    public ObservableCollection<string> AvailableModes { get; }

    /// <summary>Gets or sets the page ViewModel currently displayed in the content area.</summary>
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    /// <summary>Gets or sets the currently selected sidebar navigation item.</summary>
    public NavItemViewModel? SelectedNavItem
    {
        get => _selectedNavItem;
        set => this.RaiseAndSetIfChanged(ref _selectedNavItem, value);
    }

    /// <summary>Gets a value indicating whether the Gremlin event pipeline is active.</summary>
    public bool IsGremlinActive
    {
        get => _isGremlinActive;
        private set => this.RaiseAndSetIfChanged(ref _isGremlinActive, value);
    }

    /// <summary>
    /// Gets or sets the name of the currently selected (active) mode.
    /// Setting this from the UI switches the active mode via <see cref="IModeManager"/>.
    /// </summary>
    public string SelectedModeName
    {
        get => _selectedModeName;
        set
        {
            if (_suppressModeUpdate || value == _selectedModeName) return;
            this.RaiseAndSetIfChanged(ref _selectedModeName, value);
            if (!string.IsNullOrEmpty(value))
            {
                try { _modeManager.SwitchTo(value); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to switch mode to {Mode}", value); }
            }
        }
    }

    /// <summary>Gets or sets the display path of the currently loaded profile.</summary>
    public string ProfilePath
    {
        get => _profilePath;
        private set => this.RaiseAndSetIfChanged(ref _profilePath, value);
    }

    /// <summary>Gets a value indicating whether a profile is loaded (enables mode ComboBox and Start).</summary>
    public bool HasProfile
    {
        get => _hasProfile;
        private set => this.RaiseAndSetIfChanged(ref _hasProfile, value);
    }

    /// <summary>Gets the label for the Start/Stop toggle button.</summary>
    public string ToggleButtonLabel => _toggleButtonLabel.Value;

    /// <summary>Gets the command that toggles the Gremlin event pipeline on or off.</summary>
    public ReactiveCommand<Unit, Unit> ToggleActiveCommand { get; }

    /// <summary>Gets the command that opens a profile file dialog.</summary>
    public ReactiveCommand<Unit, Unit> OpenProfileCommand { get; }

    /// <summary>Gets the command that creates a new empty profile.</summary>
    public ReactiveCommand<Unit, Unit> NewProfileCommand { get; }

    /// <summary>Gets the command that checks for application updates via Velopack.</summary>
    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    /// <summary>
    /// Performs async startup: loads settings, initialises device manager, and optionally
    /// auto-loads the last profile. Call from the main window's <c>Opened</c> event handler.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        _settingsPage.LoadFromSettings();

        _deviceManager.Initialize();
        _devicesPage.RefreshDevices();
        _bindingsPage.RefreshDevices();
        _inputViewerPage.RefreshDevices();

        var lastPath = _settingsService.Settings.LastProfilePath;
        if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
        {
            try
            {
                var profile = await _profileRepository.LoadAsync(lastPath);
                _modeManager.Reset(profile);
                _profileState.SetProfile(profile, lastPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-load last profile from {Path}", lastPath);
            }
        }
    }

    private async Task OpenProfileAsync()
    {
        var path = await _filePicker.PickOpenFileAsync(
            "Open Profile", "Joystick Gremlin Profile", "*.json");
        if (path is null) return;

        try
        {
            var profile = await _profileRepository.LoadAsync(path);
            _modeManager.Reset(profile);
            _profileState.SetProfile(profile, path);

            _settingsService.Settings.LastProfilePath = path;
            await _settingsService.SaveAsync();
            _settingsPage.LoadFromSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open profile from {Path}", path);
        }
    }

    private void NewProfile()
    {
        var profile = new ProfileModel { Name = "New Profile" };
        profile.Modes.Add(new Mode { Name = "Default" });
        _modeManager.Reset(profile);
        _profileState.SetProfile(profile, null);
    }

    private Task ToggleActiveAsync()
    {
        if (_isGremlinActive)
        {
            _eventPipeline.Stop();
            IsGremlinActive = false;
        }
        else
        {
            var profile = _profileState.CurrentProfile;
            if (profile is null) return Task.CompletedTask;
            _eventPipeline.Start(profile);
            IsGremlinActive = true;
        }
        return Task.CompletedTask;
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var source = new GithubSource("https://github.com/DevilDogTG/JoystickGremlinSharp", null, false);
            var mgr = new UpdateManager(source);
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion is null)
            {
                _logger.LogInformation("Application is up to date");
                return;
            }
            _logger.LogInformation("Update available: {Version}", newVersion.TargetFullRelease.Version);
            await mgr.DownloadUpdatesAsync(newVersion);
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
        }
    }

    private void OnProfileChanged(object? sender, ProfileModel? profile)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasProfile = profile is not null;
            ProfilePath = _profileState.FilePath is not null
                ? Path.GetFileName(_profileState.FilePath)
                : profile is not null ? "(unsaved)" : "(no profile)";

            AvailableModes.Clear();
            if (profile is not null)
            {
                foreach (var mode in profile.Modes)
                    AvailableModes.Add(mode.Name);

                var currentMode = _modeManager.ActiveModeName;
                _suppressModeUpdate = true;
                _selectedModeName = AvailableModes.Contains(currentMode)
                    ? currentMode
                    : AvailableModes.FirstOrDefault() ?? string.Empty;
                this.RaisePropertyChanged(nameof(SelectedModeName));
                _suppressModeUpdate = false;
            }
            else
            {
                _suppressModeUpdate = true;
                _selectedModeName = string.Empty;
                this.RaisePropertyChanged(nameof(SelectedModeName));
                _suppressModeUpdate = false;
            }
        });
    }

    private void OnModeChanged(object? sender, string modeName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _suppressModeUpdate = true;
            _selectedModeName = modeName;
            this.RaisePropertyChanged(nameof(SelectedModeName));
            _suppressModeUpdate = false;
        });
    }
}
