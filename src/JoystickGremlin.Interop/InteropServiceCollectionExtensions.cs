// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Interop.Dill;
using JoystickGremlin.Interop.VJoy;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IDeviceManager, DillDeviceManager>();
        services.AddSingleton<IVirtualDeviceManager, VJoyDeviceManager>();
        return services;
    }
}
