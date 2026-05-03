// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Represents a physical force feedback output device (e.g. a steering wheel)
/// that can receive force feedback commands.
/// </summary>
public interface IForceFeedbackSink : IDisposable
{
    /// <summary>Gets the device identifier (typically the DirectInput instance GUID string).</summary>
    string DeviceId { get; }

    /// <summary>Gets the human-readable display name of the device.</summary>
    string DisplayName { get; }

    /// <summary>Gets a value indicating whether the sink is currently connected to the device.</summary>
    bool IsConnected { get; }

    /// <summary>Gets the list of force feedback effect types supported by this device.</summary>
    IReadOnlyList<FfbEffectType> SupportedEffects { get; }

    /// <summary>
    /// Connects to the physical device and prepares it for receiving force feedback commands.
    /// </summary>
    /// <param name="cancellationToken">A token that may cancel the connection attempt.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Disconnects from the physical device and releases all associated resources.</summary>
    void Disconnect();

    /// <summary>
    /// Sends a force feedback command to the physical device.
    /// </summary>
    /// <param name="command">The command to send. Must not be <c>null</c>.</param>
    void SendCommand(FfbCommand command);
}
