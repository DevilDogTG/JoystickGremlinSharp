// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using ReactiveUI.Avalonia;
using Velopack;

namespace JoystickGremlin.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must be the very first call — handles install/uninstall hooks and exits early when needed.
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Avalonia configuration — also used by the visual designer.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}
