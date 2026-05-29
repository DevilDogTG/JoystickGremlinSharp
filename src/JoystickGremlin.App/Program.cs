// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using ReactiveUI.Avalonia;
using Serilog;

namespace JoystickGremlin.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Install last-resort crash handlers before any other code runs so that
        // unhandled exceptions during startup leave a trace on disk. Serilog
        // may not be initialized yet (App.OnFrameworkInitializationCompleted
        // configures it), so these handlers also write to a plain text file.
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        WriteCrashRecord("AppDomain.UnhandledException", ex, isTerminating: e.IsTerminating);
        if (e.IsTerminating)
            Log.CloseAndFlush();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashRecord("TaskScheduler.UnobservedTaskException", e.Exception, isTerminating: false);
        e.SetObserved();
    }

    private static void WriteCrashRecord(string source, Exception? ex, bool isTerminating)
    {
        // Best-effort: also push through Serilog if it has been configured.
        try
        {
            Log.Fatal(ex, "{Source} (terminating={Terminating})", source, isTerminating);
        }
        catch
        {
            // Serilog may be uninitialized or disposed — fall through to file write.
        }

        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JoystickGremlinSharp",
                "logs");
            Directory.CreateDirectory(dir);
            var line = $"{DateTimeOffset.Now:O} [FATAL] {source} (terminating={isTerminating}){Environment.NewLine}{ex}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(dir, "crash.log"), line);
        }
        catch
        {
            // Nothing more we can do — file write itself failed.
        }
    }
}
