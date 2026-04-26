// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Modes;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Pipeline;

/// <summary>
/// Routes physical <see cref="InputEvent"/> instances through the active mode's binding table
/// to the registered action functors, applying mode inheritance lookup.
/// </summary>
public sealed class EventPipeline : IEventPipeline
{
    private readonly IDeviceManager _deviceManager;
    private readonly IModeManager _modeManager;
    private readonly IActionRegistry _actionRegistry;
    private readonly ILogger<EventPipeline> _logger;

    // Functor cache keyed by BoundAction reference. Ensures stateful functors (e.g. Toggle)
    // retain their state across events for the same binding.
    private readonly ConcurrentDictionary<BoundAction, IActionFunctor> _functorCache = new();

    private ProfileModel? _profile;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="EventPipeline"/>.
    /// </summary>
    public EventPipeline(
        IDeviceManager deviceManager,
        IModeManager modeManager,
        IActionRegistry actionRegistry,
        ILogger<EventPipeline> logger)
    {
        _deviceManager = deviceManager;
        _modeManager = modeManager;
        _actionRegistry = actionRegistry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public void Start(ProfileModel profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (IsRunning)
            Stop();

        _profile = profile;
        _deviceManager.InputReceived += OnInputReceived;
        IsRunning = true;

        _logger.LogTrace("Event pipeline started for profile '{Profile}'", profile.Name);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (!IsRunning)
            return;

        _deviceManager.InputReceived -= OnInputReceived;
        IsRunning = false;
        _profile = null;
        _functorCache.Clear();

        _logger.LogTrace("Event pipeline stopped");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
    }

    private void OnInputReceived(object? sender, InputEvent rawEvent)
    {
        if (_profile is null)
            return;

        // Enrich the event with the current mode name.
        var enriched = rawEvent with { Mode = _modeManager.ActiveModeName };

        // Resolve the inheritance chain for the active mode.
        var chain = _modeManager.GetInheritanceChain(enriched.Mode, _profile);

        // Find matching bindings from most-specific to least-specific mode.
        List<BoundAction>? actions = null;
        foreach (var modeName in chain)
        {
            var mode = _profile.Modes.FirstOrDefault(
                m => string.Equals(m.Name, modeName, StringComparison.Ordinal));

            if (mode is null)
                continue;

            var binding = mode.Bindings.FirstOrDefault(b =>
                b.DeviceGuid == enriched.DeviceGuid &&
                b.InputType == enriched.InputType &&
                b.Identifier == enriched.Identifier);

            if (binding is not null)
            {
                actions = binding.Actions;
                break;
            }
        }

        if (actions is null || actions.Count == 0)
            return;

        // Dispatch each bound action's functor asynchronously, fire-and-forget with logging.
        foreach (var boundAction in actions)
        {
            var descriptor = _actionRegistry.Resolve(boundAction.ActionTag);
            if (descriptor is null)
            {
                _logger.LogWarning("No descriptor registered for action tag '{Tag}'", boundAction.ActionTag);
                continue;
            }

            var functor = _functorCache.GetOrAdd(
                boundAction,
                ba => descriptor.CreateFunctor(ba.Configuration));
            _ = Task.Run(async () =>
            {
                try
                {
                    await functor.ExecuteAsync(enriched);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Functor for action '{Tag}' threw an exception", boundAction.ActionTag);
                }
            });
        }
    }
}
