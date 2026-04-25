// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JoystickGremlin.App.ViewModels;
using JoystickGremlin.App.Views;
using JoystickGremlin.Core;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Profile;
using JoystickGremlin.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.App;

public partial class App : Application
{
    private ServiceProvider? _services;

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowVm = _services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = mainWindowVm };
            desktop.MainWindow = mainWindow;

            var filePickerService = _services.GetRequiredService<FilePickerService>();
            mainWindow.Opened += async (_, _) =>
            {
                filePickerService.SetTopLevel(mainWindow);
                await mainWindowVm.InitializeAsync();
            };

            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        // Page ViewModels — singletons so state persists when navigating between pages.
        services.AddSingleton<DevicesPageViewModel>();
        services.AddSingleton<ProfilePageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<BindingsPageViewModel>();

        // Main window ViewModel — transient; resolved once in OnFrameworkInitializationCompleted.
        services.AddTransient<MainWindowViewModel>();

        // File picker service — concrete type also registered so SetTopLevel can be called.
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());

        // Interop layer: DILL physical device input + vJoy virtual device output
        services.AddInteropServices();

        // Core domain services: profile repository, action registry, settings, pipeline
        services.AddCoreServices();

        return services;
    }
}

