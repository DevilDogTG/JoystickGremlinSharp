// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Events;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Pipeline;

/// <summary>
/// Wires together physical device input events, mode resolution, and action functor dispatch.
/// </summary>
public interface IEventPipeline : IDisposable
{
    /// <summary>
    /// Starts the pipeline against the given profile.
    /// Subscribes to <see cref="Devices.IDeviceManager.InputReceived"/> and begins dispatching.
    /// </summary>
    /// <param name="profile">The active profile providing mode/binding configuration.</param>
    void Start(ProfileModel profile);

    /// <summary>
    /// Stops the pipeline and unsubscribes from device events. Does not dispose managed resources.
    /// </summary>
    void Stop();

    /// <summary>Gets a value indicating whether the pipeline is currently running.</summary>
    bool IsRunning { get; }
}
