// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the main application window. Owns the top toolbar state
/// (profile path, active mode, start/stop) and drives sidebar navigation.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ObservableAsPropertyHelper<string> _toggleButtonLabel;
    private ViewModelBase _currentPage;
    private NavItemViewModel? _selectedNavItem;
    private bool _isGremlinActive;
    private string _activeModeName = "Default";
    private string _profilePath = "(no profile)";

    /// <summary>
    /// Initializes a new instance of <see cref="MainWindowViewModel"/>.
    /// </summary>
    public MainWindowViewModel(
        DevicesPageViewModel devicesPage,
        ProfilePageViewModel profilePage,
        SettingsPageViewModel settingsPage)
    {
        var items = new[]
        {
            new NavItemViewModel { Title = "Devices",  Icon = "🎮", Page = devicesPage  },
            new NavItemViewModel { Title = "Profile",  Icon = "📋", Page = profilePage  },
            new NavItemViewModel { Title = "Settings", Icon = "⚙️", Page = settingsPage },
        };

        NavItems = new ObservableCollection<NavItemViewModel>(items);
        _currentPage = devicesPage;
        _selectedNavItem = items[0];

        ToggleActiveCommand = ReactiveCommand.Create(() => { IsGremlinActive = !_isGremlinActive; });
        OpenProfileCommand  = ReactiveCommand.Create(() => { /* wired in ui-wiring step */ });
        NewProfileCommand   = ReactiveCommand.Create(() => { /* wired in ui-wiring step */ });

        _toggleButtonLabel = this.WhenAnyValue(x => x.IsGremlinActive)
            .Select(active => active ? "⏹  Stop" : "▶  Start")
            .ToProperty(this, x => x.ToggleButtonLabel, initialValue: "▶  Start");

        _ = this.WhenAnyValue(x => x.SelectedNavItem)
            .WhereNotNull()
            .Subscribe(item => CurrentPage = item.Page);
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

    /// <summary>Gets or sets the name of the currently active mode.</summary>
    public string ActiveModeName
    {
        get => _activeModeName;
        set => this.RaiseAndSetIfChanged(ref _activeModeName, value);
    }

    /// <summary>Gets or sets the file path of the currently loaded profile.</summary>
    public string ProfilePath
    {
        get => _profilePath;
        set => this.RaiseAndSetIfChanged(ref _profilePath, value);
    }

    /// <summary>Gets the label for the Start/Stop toggle button ("▶ Start" or "⏹ Stop").</summary>
    public string ToggleButtonLabel => _toggleButtonLabel.Value;

    /// <summary>Gets the command that toggles the Gremlin event pipeline on or off.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ToggleActiveCommand { get; }

    /// <summary>Gets the command that opens a profile file dialog.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> OpenProfileCommand { get; }

    /// <summary>Gets the command that creates a new empty profile.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> NewProfileCommand { get; }
}
