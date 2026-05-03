// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Represents a source of force feedback commands from a virtual joystick device.
/// The <see cref="CommandReceived"/> event fires on a native thread; consumers must
/// ensure thread safety when handling it.
/// </summary>
public interface IForceFeedbackSource : IDisposable
{
    /// <summary>Gets the vJoy device ID this source monitors.</summary>
    uint VJoyDeviceId { get; }

    /// <summary>Gets a value indicating whether the source is currently active.</summary>
    bool IsRunning { get; }

    /// <summary>Gets a value indicating whether the underlying device supports force feedback.</summary>
    bool IsFfbCapable { get; }

    /// <summary>
    /// Raised when a force feedback command is received from the vJoy driver.
    /// <para>
    /// <b>Warning:</b> This event is raised on a native callback thread.
    /// Subscribers must be thread-safe. Do not perform blocking operations in handlers.
    /// </para>
    /// </summary>
    event EventHandler<FfbCommand> CommandReceived;

    /// <summary>Starts listening for force feedback commands from the vJoy driver.</summary>
    void Start();

    /// <summary>Stops listening for force feedback commands.</summary>
    void Stop();
}
