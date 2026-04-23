// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Modes;
using JoystickGremlin.Core.Pipeline;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.DependencyInjection;

namespace JoystickGremlin.Core;

/// <summary>
/// Extension methods for registering Core domain services with the DI container.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Core domain services: profile repository, action registry, and settings service.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddTransient<IProfileRepository, ProfileRepository>();
        services.AddSingleton<IActionRegistry, ActionRegistry>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IModeManager, ModeManager>();
        services.AddSingleton<IEventPipeline, EventPipeline>();

        // Built-in vJoy action descriptors — auto-registered into IActionRegistry at startup
        services.AddSingleton<IActionDescriptor, VJoyAxisDescriptor>();
        services.AddSingleton<IActionDescriptor, VJoyButtonDescriptor>();
        services.AddSingleton<IActionDescriptor, VJoyHatDescriptor>();

        return services;
    }
}
