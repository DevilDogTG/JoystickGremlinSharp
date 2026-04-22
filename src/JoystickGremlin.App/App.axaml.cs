// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JoystickGremlin.App.ViewModels;
using JoystickGremlin.App.Views;
using Microsoft.Extensions.DependencyInjection;

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        // TODO: Register Core services (DeviceManager, EventPipeline, ProfileRepository, etc.)
        // TODO: Register Interop services (VJoyDevice, DillDevice wrappers)

        return services;
    }
}