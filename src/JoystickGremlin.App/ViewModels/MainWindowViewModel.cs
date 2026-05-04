// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.App.ViewModels.InputViewer;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Devices;
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
/// (profile quick-switch, start/stop) and drives sidebar navigation.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IEventPipeline _eventPipeline;
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileState _profileState;
    private readonly IProfileLibrary _profileLibrary;
    private readonly ISettingsService _settingsService;
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
    private ProfileEntry? _selectedProfileEntry;

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
        IProfileRepository profileRepository,
        IProfileState profileState,
        IProfileLibrary profileLibrary,
        ISettingsService settingsService,
        IDeviceManager deviceManager,
        ILogger<MainWindowViewModel> logger)
    {
        _eventPipeline = eventPipeline;
        _profileRepository = profileRepository;
        _profileState = profileState;
        _profileLibrary = profileLibrary;
        _settingsService = settingsService;
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
        AvailableProfileEntries = new ObservableCollection<ProfileEntry>();
        _currentPage = devicesPage;
        _selectedNavItem = navItems[0];

        _toggleButtonLabel = this.WhenAnyValue(x => x.IsGremlinActive)
            .Select(active => active ? "⏹  Stop" : "▶  Start")
            .ToProperty(this, x => x.ToggleButtonLabel, initialValue: "▶  Start");

        _ = this.WhenAnyValue(x => x.SelectedNavItem)
            .WhereNotNull()
            .Subscribe(item => CurrentPage = item.Page);

        // When user picks a different profile in the dropdown, load it.
        _ = this.WhenAnyValue(x => x.SelectedProfileEntry)
            .WhereNotNull()
            .Subscribe(entry => _ = LoadProfileEntryAsync(entry));

        ToggleActiveCommand    = ReactiveCommand.CreateFromTask(ToggleActiveAsync);
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync);

        _profileLibrary.LibraryChanged += OnLibraryChanged;
        _profileState.ProfileChanged   += OnProfileChanged;
    }

    /// <summary>Gets the sidebar navigation items.</summary>
    public ObservableCollection<NavItemViewModel> NavItems { get; }

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

    /// <summary>Gets the flat list of all available profile entries for the quick-switch dropdown.</summary>
    public ObservableCollection<ProfileEntry> AvailableProfileEntries { get; }

    /// <summary>Gets or sets the selected profile entry in the quick-switch dropdown.</summary>
    public ProfileEntry? SelectedProfileEntry
    {
        get => _selectedProfileEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedProfileEntry, value);
    }

    /// <summary>Gets the label for the Start/Stop toggle button.</summary>
    public string ToggleButtonLabel => _toggleButtonLabel.Value;

    /// <summary>Gets the command that toggles the Gremlin event pipeline on or off.</summary>
    public ReactiveCommand<Unit, Unit> ToggleActiveCommand { get; }

    /// <summary>Gets the command that checks for application updates via Velopack.</summary>
    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    /// <summary>
    /// Performs async startup: loads settings, initialises device manager, and scans the profile library.
    /// Call from the main window's <c>Opened</c> event handler.
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing main window and device services");

        await _settingsService.LoadAsync();
        _settingsPage.LoadFromSettings();

        _deviceManager.Initialize();
        _devicesPage.RefreshDevices();
        _bindingsPage.RefreshDevices();
        _inputViewerPage.RefreshDevices();

        _logger.LogInformation("Device manager initialized with {DeviceCount} physical devices", _deviceManager.Devices.Count);

        await _profileLibrary.ScanAsync();
        RebuildProfileEntries();

        var activeProfilePath = _settingsService.Settings.ActiveProfilePath;
        if (!string.IsNullOrWhiteSpace(activeProfilePath))
        {
            var matchingEntry = AvailableProfileEntries.FirstOrDefault(entry =>
                string.Equals(entry.FilePath, activeProfilePath, StringComparison.OrdinalIgnoreCase));
            if (matchingEntry is not null)
                SelectedProfileEntry = matchingEntry;
        }
    }

    private async Task LoadProfileEntryAsync(ProfileEntry entry)
    {
        try
        {
            var profile = await _profileRepository.LoadAsync(entry.FilePath);
            _profileState.SetProfile(profile, entry.FilePath);
            _settingsService.Settings.ActiveProfilePath = entry.FilePath;
            await _settingsService.SaveAsync();
            _logger.LogInformation("Loaded profile {ProfileName} from {Path}", entry.Name, entry.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile from {Path}", entry.FilePath);
        }
    }

    private Task ToggleActiveAsync()
    {
        if (_isGremlinActive)
        {
            _logger.LogInformation("Stopping Gremlin event pipeline");
            _eventPipeline.Stop();
            IsGremlinActive = false;
        }
        else
        {
            var profile = _profileState.CurrentProfile;
            if (profile is null) return Task.CompletedTask;
            _logger.LogInformation("Starting Gremlin event pipeline for profile {Profile}", profile.Name);
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

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RebuildProfileEntries);
    }

    private void RebuildProfileEntries()
    {
        var current = _selectedProfileEntry?.FilePath;
        AvailableProfileEntries.Clear();
        foreach (var entry in _profileLibrary.Entries)
            AvailableProfileEntries.Add(entry);

        // Restore selection by file path so the ComboBox stays in sync.
        _selectedProfileEntry = current is not null
            ? AvailableProfileEntries.FirstOrDefault(e => e.FilePath == current)
            : null;
        this.RaisePropertyChanged(nameof(SelectedProfileEntry));
    }

    private void OnProfileChanged(object? sender, ProfileModel? profile)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var filePath = _profileState.FilePath;
            if (filePath is not null)
            {
                var match = AvailableProfileEntries.FirstOrDefault(e => e.FilePath == filePath);
                if (match is not null && !ReferenceEquals(match, _selectedProfileEntry))
                {
                    _selectedProfileEntry = match;
                    this.RaisePropertyChanged(nameof(SelectedProfileEntry));
                }
            }
        });
    }
}
