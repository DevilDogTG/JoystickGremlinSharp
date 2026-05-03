// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JoystickGremlin.App.Services;
using JoystickGremlin.App.ViewModels;
using JoystickGremlin.App.ViewModels.InputViewer;
using JoystickGremlin.App.Views;
using JoystickGremlin.Core;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Profile;
using JoystickGremlin.Core.Startup;
using JoystickGremlin.Interop;
using JoystickGremlin.Interop.VJoy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace JoystickGremlin.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private MainWindow? _mainWindow;

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowVm = _services.GetRequiredService<MainWindowViewModel>();
            _mainWindow = new MainWindow { DataContext = mainWindowVm };
            desktop.MainWindow = _mainWindow;

            var settingsService = _services.GetRequiredService<ISettingsService>();
            var filePickerService = _services.GetRequiredService<FilePickerService>();

            _mainWindow.Opened += async (_, _) =>
            {
                filePickerService.SetTopLevel(_mainWindow);

                // Check vJoy prerequisite before initialising — show a warning dialog if not met.
                var vjoyCheck = VJoyPrerequisiteChecker.Check();
                if (!vjoyCheck.IsOk)
                {
                    var dialog = new VJoyWarningDialog(vjoyCheck);
                    await dialog.ShowDialog(_mainWindow);
                }

                await mainWindowVm.InitializeAsync();

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
                _services.Dispose();
                Log.CloseAndFlush();
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
    private void Exit_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Disable close-to-tray so the Closing handler lets the window close for real.
            if (_services is not null)
                _services.GetRequiredService<ISettingsService>().Settings.CloseToTray = false;

            desktop.Shutdown();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
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

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        // Page ViewModels — singletons so state persists when navigating between pages.
        services.AddSingleton<DevicesPageViewModel>();
        services.AddSingleton<ProfilePageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<BindingsPageViewModel>();
        services.AddSingleton<InputViewerPageViewModel>();

        // Main window ViewModel — transient; resolved once in OnFrameworkInitializationCompleted.
        services.AddTransient<MainWindowViewModel>();

        // File picker service — concrete type also registered so SetTopLevel can be called.
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());

        // Process monitor orchestration service.
        services.AddSingleton<ProcessMonitorService>();

        // Interop layer: DILL physical device input + vJoy virtual device output
        services.AddInteropServices();

        // Core domain services: profile repository, action registry, settings, pipeline
        services.AddCoreServices();

        return services;
    }
}

