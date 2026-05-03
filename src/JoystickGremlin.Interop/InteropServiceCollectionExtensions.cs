// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Actions.Keyboard;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.ForceFeedback;
using JoystickGremlin.Core.ProcessMonitor;
using JoystickGremlin.Core.Startup;
using JoystickGremlin.Interop.Dill;
using JoystickGremlin.Interop.Keyboard;
using JoystickGremlin.Interop.Moza;
using JoystickGremlin.Interop.ProcessMonitor;
using JoystickGremlin.Interop.Startup;
using JoystickGremlin.Interop.VJoy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop;

/// <summary>
/// Extension methods for registering Interop layer services with the DI container.
/// </summary>
public static class InteropServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DILL and vJoy interop services.
    /// <list type="bullet">
    ///   <item><see cref="IDeviceManager"/> → <see cref="DillDeviceManager"/> (Singleton)</item>
    ///   <item><see cref="IVirtualDeviceManager"/> → <see cref="VJoyDeviceManager"/> (Singleton)</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddInteropServices(this IServiceCollection services)
    {
        // Must run before any P/Invoke into vJoyInterface.dll so the resolver
        // can redirect loads to the installed (driver-matched) DLL.
        VJoyNativeLibraryLoader.EnsureLoaded();

        services.AddSingleton<IDeviceManager, DillDeviceManager>();
        services.AddSingleton<IVirtualDeviceManager, VJoyDeviceManager>();

        // Override Core's no-op defaults with real Windows implementations.
        services.AddSingleton<SendInputKeyboardSimulator>();
        services.TryAddSingleton<IKeyboardSimulator>(sp => sp.GetRequiredService<SendInputKeyboardSimulator>());
        services.TryAddSingleton<IProcessMonitor, WindowsProcessMonitor>();
        services.TryAddSingleton<IStartupService, WindowsStartupService>();

        // Register FFB services — only active if EnableFfbBridge=true in settings.
        services.AddSingleton<VJoyFfbSource>(sp => new VJoyFfbSource(
            sp.GetRequiredService<ISettingsService>().Settings.FfbVJoyDeviceId,
            sp.GetRequiredService<ILogger<VJoyFfbSource>>()));
        services.AddSingleton<MozaFfbSink>();
        services.TryAddSingleton<IForceFeedbackBridge>(sp =>
            new ForceFeedbackBridge(
                sp.GetRequiredService<VJoyFfbSource>(),
                sp.GetRequiredService<MozaFfbSink>(),
                sp.GetRequiredService<ILogger<ForceFeedbackBridge>>()));

        return services;
    }
}
