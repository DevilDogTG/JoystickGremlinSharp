// SPDX-License-Identifier: GPL-3.0-only

using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JoystickGremlin.App.Services;
using JoystickGremlin.App.ViewModels;
using JoystickGremlin.App.Views;
using JoystickGremlin.Core;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.HidHide;
using JoystickGremlin.Core.Profile;
using JoystickGremlin.Core.Startup;
using JoystickGremlin.Interop;
using JoystickGremlin.Interop.HidHide;
using JoystickGremlin.Interop.VJoy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace JoystickGremlin.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private MainWindow? _mainWindow;
    private TrayMenuService? _trayMenuService;
    private int _isInitialized;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = ConfigureServices().BuildServiceProvider();

        // Register all IActionDescriptor implementations into the registry.
        var registry = _services.GetRequiredService<IActionRegistry>();
        foreach (var descriptor in _services.GetServices<IActionDescriptor>())
            registry.Register(descriptor);

        // Start the process monitor service so it begins watching for game processes.
        _services.GetRequiredService<ProcessMonitorService>().Start();

        // Resolve the FFB auto-bridge so its constructor subscribes to pipeline events.
        _ = _services.GetRequiredService<JoystickGremlin.Core.ForceFeedback.FfbAutoBridgeService>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowVm = _services.GetRequiredService<MainWindowViewModel>();
            _mainWindow = new MainWindow { DataContext = mainWindowVm };
            desktop.MainWindow = _mainWindow;

            var settingsService = _services.GetRequiredService<ISettingsService>();
            var filePickerService = _services.GetRequiredService<FilePickerService>();
            var processPickerService = _services.GetRequiredService<ProcessPickerDialogService>();

            _mainWindow.Opened += async (_, _) =>
            {
                // Guard: only run initialization once. In Avalonia on Windows, Opened fires
                // again each time Show() is called on a hidden window, so without this guard
                // a tray-restore Show() would re-trigger the StartMinimized hide and the
                // window would immediately hide again. Interlocked ensures the check-and-set
                // is atomic across any thread that could invoke Show().
                if (Interlocked.CompareExchange(ref _isInitialized, 1, 0) == 1) return;

                filePickerService.SetTopLevel(_mainWindow);
                processPickerService.SetOwner(_mainWindow);

                // Run both prerequisite checks and show a combined warning if any fail.
                var vjoyCheck    = VJoyPrerequisiteChecker.Check();
                var hidHideCheck = HidHidePrerequisiteChecker.Check();

                if (!vjoyCheck.IsOk || !hidHideCheck.IsInstalled)
                {
                    var dialog = new PrerequisitesWarningDialog(
                        vjoyCheck.IsOk    ? null : vjoyCheck,
                        hidHideCheck.IsInstalled ? null : hidHideCheck);
                    await dialog.ShowDialog(_mainWindow);
                }

                // Perform HidHide crash-recovery revert and auto-whitelist own executable.
                var hidHideManager = _services.GetRequiredService<IHidHideManager>();
                await hidHideManager.InitializeAsync();

                await mainWindowVm.InitializeAsync();
                AttachTrayMenu(mainWindowVm);

                // After settings are loaded, hide window if start-minimized is set.
                if (settingsService.Settings.StartMinimized)
                    _mainWindow.Hide();
            };

            // Intercept window close: minimize to tray when CloseToTray is enabled.
            _mainWindow.Closing += (_, e) =>
            {
                if (settingsService.Settings.CloseToTray)
                {
                    e.Cancel = true;
                    _mainWindow.Hide();
                }
            };

            desktop.Exit += (_, _) =>
            {
                DisposeApp();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ── Tray icon event handlers ─────────────────────────────────────────────

    /// <summary>Restores the main window when the tray icon is double-clicked.</summary>
    private void TrayIcon_Clicked(object? sender, EventArgs e) => ShowMainWindow();

    /// <summary>Restores the main window from the tray context menu.</summary>
    private void ShowWindow_Click(object? sender, EventArgs e) => ShowMainWindow();

    /// <summary>Exits the application from the tray context menu.</summary>
    private void Exit_Click(object? sender, EventArgs e) => ExitApplication();

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void AttachTrayMenu(MainWindowViewModel mainWindowVm)
    {
        if (_services is null)
            return;

        var trayLogger = _services.GetRequiredService<ILogger<TrayMenuService>>();
        _trayMenuService = new TrayMenuService(
            mainWindowVm,
            _services.GetRequiredService<IProfileLibrary>(),
            trayLogger,
            showWindowCallback: ShowMainWindow,
            exitCallback: ExitApplication);

        var icons = TrayIcon.GetIcons(this);
        if (icons is { Count: > 0 })
            icons[0].Menu = _trayMenuService.Menu;
    }

    private void ExitApplication()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        if (_services is not null)
            _services.GetRequiredService<ISettingsService>().Settings.CloseToTray = false;

        desktop.Shutdown();
    }

    private void DisposeApp()
    {
        _trayMenuService?.Dispose();
        _services?.Dispose();
        Log.CloseAndFlush();
    }

    // ── DI configuration ────────────────────────────────────────────────────

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JoystickGremlinSharp",
            "logs");
        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "JoystickGremlinSharp-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Diagnostic logging enabled. Log files are written to {LogFilePath}", logFilePath);

        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            var isElevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            Log.Information("Process elevation: IsAdministrator={IsElevated}, User={User}", isElevated, identity.Name);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to determine process elevation status");
        }

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        // Page ViewModels — singletons so state persists when navigating between pages.
        services.AddSingleton<ControllerSetupPageViewModel>();
        services.AddSingleton<ProfilePageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<AutoLoadPageViewModel>();
        services.AddSingleton<BindingsPageViewModel>();
        services.AddSingleton<VirtualDevicesPageViewModel>();
        services.AddSingleton<AboutPageViewModel>();

        // Main window ViewModel — transient; resolved once in OnFrameworkInitializationCompleted.
        services.AddTransient<MainWindowViewModel>();

        // File picker service — concrete type also registered so SetTopLevel can be called.
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());

        // Process picker dialog service — concrete type also registered so SetOwner can be called.
        services.AddSingleton<ProcessPickerDialogService>();
        services.AddSingleton<IProcessPickerDialogService>(sp => sp.GetRequiredService<ProcessPickerDialogService>());

        // Process monitor orchestration service.
        services.AddSingleton<ProcessMonitorService>();

        // Interop layer: DILL physical device input + vJoy virtual device output
        services.AddInteropServices();

        // Core domain services: profile repository, action registry, settings, pipeline
        services.AddCoreServices();

        return services;
    }
}

