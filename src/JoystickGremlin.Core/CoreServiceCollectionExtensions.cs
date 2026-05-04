// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Actions.Keyboard;
using JoystickGremlin.Core.Actions.Macro;
using JoystickGremlin.Core.ProcessMonitor;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ForceFeedback;
using JoystickGremlin.Core.Pipeline;
using JoystickGremlin.Core.Profile;
using JoystickGremlin.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddSingleton<IEventPipeline, EventPipeline>();
        services.AddSingleton<IProfileState, ProfileState>();
        services.AddSingleton<IProfileLibrary, ProfileLibrary>();

        // Built-in vJoy action descriptors — auto-registered into IActionRegistry at startup
        services.AddSingleton<IActionDescriptor, VJoyAxisDescriptor>();
        services.AddSingleton<IActionDescriptor, VJoyButtonDescriptor>();
        services.AddSingleton<IActionDescriptor, VJoyHatDescriptor>();
        services.AddSingleton<IActionDescriptor, ButtonsToHatDescriptor>();
        services.AddSingleton<IActionDescriptor, ButtonsToAxesDescriptor>();
        services.AddSingleton<IActionDescriptor, HatToAxisDescriptor>();

        // Built-in macro and keyboard-mapping descriptors
        services.AddSingleton<IActionDescriptor, MacroActionDescriptor>();
        services.AddSingleton<IActionDescriptor, MapToKeyboardActionDescriptor>();

        // Keyboard simulator — NullKeyboardSimulator by default; override in App or Interop for real input.
        services.TryAddSingleton<IKeyboardSimulator, NullKeyboardSimulator>();

        // Process monitor — NullProcessMonitor by default; override in Interop for real window tracking.
        services.TryAddSingleton<IProcessMonitor, NullProcessMonitor>();

        // Startup service — NullStartupService by default; override in Interop for real registry integration.
        services.TryAddSingleton<IStartupService, NullStartupService>();

        // FFB bridge — NullForceFeedbackBridge by default; override in Interop for real bridge.
        services.TryAddSingleton<IForceFeedbackBridge, NullForceFeedbackBridge>();

        return services;
    }
}
