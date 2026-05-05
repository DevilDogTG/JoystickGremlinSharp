// SPDX-License-Identifier: GPL-3.0-only

using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using JoystickGremlin.App.ViewModels;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace JoystickGremlin.App.Services;

/// <summary>
/// Builds and maintains the system-tray context menu, keeping it in sync with application state.
/// Avalonia's <see cref="NativeMenu"/> does not support data-binding, so this service manages
/// menu items imperatively and subscribes to reactive state changes.
/// </summary>
public sealed class TrayMenuService : IDisposable
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly IProfileLibrary _profileLibrary;
    private readonly ILogger<TrayMenuService> _logger;
    private readonly CompositeDisposable _subscriptions = [];

    private readonly NativeMenuItem _toggleMappingItem;
    private readonly NativeMenuItem _liveUpdateItem;
    private readonly NativeMenuItem _profilesItem;

    // Raised by the host (App) to show the main window — avoids a direct back-reference.
    private readonly Action _showWindowCallback;
    private readonly Action _exitCallback;

    /// <summary>
    /// Initializes a new instance of <see cref="TrayMenuService"/> and builds the tray menu.
    /// </summary>
    public TrayMenuService(
        MainWindowViewModel mainWindowVm,
        IProfileLibrary profileLibrary,
        ILogger<TrayMenuService> logger,
        Action showWindowCallback,
        Action exitCallback)
    {
        _mainWindowVm     = mainWindowVm;
        _profileLibrary   = profileLibrary;
        _logger           = logger;
        _showWindowCallback = showWindowCallback;
        _exitCallback     = exitCallback;

        // Build static skeleton items.
        _toggleMappingItem = new NativeMenuItem { Header = MappingLabel(false) };
        _toggleMappingItem.Click += OnToggleMappingClicked;

        _liveUpdateItem = new NativeMenuItem
        {
            Header     = "Live Update",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = mainWindowVm.IsLiveInputRefreshEnabled,
        };
        _liveUpdateItem.Click += OnLiveUpdateClicked;

        _profilesItem = new NativeMenuItem { Header = "Profiles" };

        var showItem = new NativeMenuItem { Header = "Show" };
        showItem.Click += (_, _) => showWindowCallback();

        var exitItem = new NativeMenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => exitCallback();

        // Assemble the top-level menu.
        Menu = new NativeMenu();
        Menu.Add(_toggleMappingItem);
        Menu.Add(new NativeMenuItemSeparator());
        Menu.Add(_liveUpdateItem);
        Menu.Add(new NativeMenuItemSeparator());
        Menu.Add(_profilesItem);
        Menu.Add(new NativeMenuItemSeparator());
        Menu.Add(showItem);
        Menu.Add(new NativeMenuItemSeparator());
        Menu.Add(exitItem);

        // Populate profiles submenu for the first time.
        RebuildProfileSubmenu();

        // Subscribe to reactive state changes.
        _subscriptions.Add(
            mainWindowVm.WhenAnyValue(x => x.IsGremlinActive)
                .Subscribe(active =>
                {
                    Dispatcher.UIThread.Post(() => _toggleMappingItem.Header = MappingLabel(active));
                }));

        _subscriptions.Add(
            mainWindowVm.WhenAnyValue(x => x.IsLiveInputRefreshEnabled)
                .Subscribe(enabled =>
                {
                    Dispatcher.UIThread.Post(() => _liveUpdateItem.IsChecked = enabled);
                }));

        _subscriptions.Add(
            mainWindowVm.WhenAnyValue(x => x.SelectedProfileEntry)
                .Subscribe(_ => Dispatcher.UIThread.Post(RefreshProfileCheckmarks)));

        _profileLibrary.LibraryChanged += OnLibraryChanged;
    }

    /// <summary>Gets the built tray context menu, ready to assign to a <see cref="TrayIcon"/>.</summary>
    public NativeMenu Menu { get; }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string MappingLabel(bool active) => active ? "⏹  Stop Mapping" : "▶  Start Mapping";

    private void OnToggleMappingClicked(object? sender, EventArgs e)
    {
        _mainWindowVm.ToggleActiveCommand.Execute().Subscribe(
            _ => { },
            ex => _logger.LogError(ex, "Error toggling mapping from tray"));
    }

    private void OnLiveUpdateClicked(object? sender, EventArgs e)
    {
        _mainWindowVm.IsLiveInputRefreshEnabled = !_mainWindowVm.IsLiveInputRefreshEnabled;
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RebuildProfileSubmenu);
    }

    /// <summary>Rebuilds the Profiles submenu from the current library entries.</summary>
    private void RebuildProfileSubmenu()
    {
        var submenu = new NativeMenu();
        var selectedPath = _mainWindowVm.SelectedProfileEntry?.FilePath;

        // Root-level profiles (no category).
        foreach (var entry in _profileLibrary.Entries.Where(e => e.Category is null))
        {
            submenu.Add(BuildProfileItem(entry, selectedPath));
        }

        // Category submenus.
        var categories = _profileLibrary.Entries
            .Where(e => e.Category is not null)
            .GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in categories)
        {
            if (submenu.Items.Count > 0)
                submenu.Add(new NativeMenuItemSeparator());

            var categorySubmenu = new NativeMenu();
            foreach (var entry in group.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                categorySubmenu.Add(BuildProfileItem(entry, selectedPath));

            var categoryItem = new NativeMenuItem
            {
                Header = group.Key,
                Menu   = categorySubmenu,
            };
            submenu.Add(categoryItem);
        }

        _profilesItem.Menu = submenu;
    }

    /// <summary>
    /// Updates only the checkmark state of profile items without rebuilding the full submenu.
    /// Called when the selected profile changes but the library has not changed.
    /// </summary>
    private void RefreshProfileCheckmarks()
    {
        if (_profilesItem.Menu is null) return;
        var selectedPath = _mainWindowVm.SelectedProfileEntry?.FilePath;
        ApplyCheckmarks(_profilesItem.Menu, selectedPath);
    }

    private static void ApplyCheckmarks(NativeMenu menu, string? selectedPath)
    {
        foreach (var item in menu.Items)
        {
            if (item is NativeMenuItem menuItem)
            {
                if (menuItem.Menu is not null)
                {
                    // Category submenu — recurse.
                    ApplyCheckmarks(menuItem.Menu, selectedPath);
                }
                else if (menuItem.CommandParameter is string filePath)
                {
                    menuItem.IsChecked = string.Equals(filePath, selectedPath, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private NativeMenuItem BuildProfileItem(ProfileEntry entry, string? selectedPath)
    {
        var item = new NativeMenuItem
        {
            Header          = entry.Name,
            ToggleType      = MenuItemToggleType.CheckBox,
            IsChecked       = string.Equals(entry.FilePath, selectedPath, StringComparison.OrdinalIgnoreCase),
            CommandParameter = entry.FilePath,
        };
        item.Click += (_, _) => OnProfileItemClicked(entry);
        return item;
    }

    private void OnProfileItemClicked(ProfileEntry entry)
    {
        // Setting SelectedProfileEntry on the ViewModel triggers the existing LoadProfileEntryAsync flow.
        Dispatcher.UIThread.Post(() =>
        {
            _mainWindowVm.SelectedProfileEntry = entry;
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _profileLibrary.LibraryChanged -= OnLibraryChanged;
        _subscriptions.Dispose();
    }
}
