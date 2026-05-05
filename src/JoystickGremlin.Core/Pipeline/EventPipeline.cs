// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Pipeline;

/// <summary>
/// Routes physical <see cref="InputEvent"/> instances through the active profile's binding table
/// to the registered action functors.
/// </summary>
public sealed class EventPipeline : IEventPipeline
{
    private readonly IDeviceManager _deviceManager;
    private readonly IActionRegistry _actionRegistry;
    private readonly IProfileState _profileState;
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
        IActionRegistry actionRegistry,
        IProfileState profileState,
        ILogger<EventPipeline> logger)
    {
        _deviceManager    = deviceManager;
        _actionRegistry   = actionRegistry;
        _profileState     = profileState;
        _logger           = logger;

        _profileState.ProfileChanged += OnProfileChanged;
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
        _profileState.ProfileChanged -= OnProfileChanged;
        _disposed = true;
    }

    private void OnProfileChanged(object? sender, ProfileModel? profile)
    {
        _profile = profile;
        _functorCache.Clear();

        _logger.LogTrace(
            "Event pipeline refreshed action functor cache after profile change; running={IsRunning}, profile={ProfileName}",
            IsRunning,
            profile?.Name ?? "(null)");
    }

    private void OnInputReceived(object? sender, InputEvent inputEvent)
    {
        if (_profile is null)
            return;

        var binding = _profile.Bindings.FirstOrDefault(b =>
            b.DeviceGuid == inputEvent.DeviceGuid &&
            b.InputType  == inputEvent.InputType  &&
            b.Identifier == inputEvent.Identifier);

        if (binding is null || binding.Actions.Count == 0)
            return;

        // Dispatch each bound action's functor. Synchronous functors (the common case, e.g. axis/button)
        // execute inline on the callback thread to avoid Task.Run overhead at high polling rates.
        // Truly async functors are backgrounded with error logging via a continuation.
        foreach (var boundAction in binding.Actions)
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

            try
            {
                var task = functor.ExecuteAsync(inputEvent);
                if (!task.IsCompletedSuccessfully)
                {
                    var tag = boundAction.ActionTag;
                    _ = task.ContinueWith(
                        t => _logger.LogError(t.Exception, "Functor for action '{Tag}' threw an exception", tag),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Functor for action '{Tag}' threw an exception", boundAction.ActionTag);
            }
        }
    }
}
