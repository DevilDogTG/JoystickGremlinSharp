// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.EmuWheel;

/// <summary>
/// Manages the lifecycle of the EmuWheel virtual device, including USB identity spoofing
/// and axis/button/hat output. The EmuWheel backend spoofs a vJoy device slot to present
/// as a real steering wheel (VID/PID of a supported model) so that games detect it as a
/// proper wheel input device.
/// </summary>
/// <remarks>
/// <para>
/// The spoof is <b>profile-scoped</b>: call <see cref="ApplySpoofAsync"/> when a profile
/// containing <c>emuwheel-*</c> actions starts, and <see cref="RestoreAsync"/> when the
/// pipeline stops or the app exits. This limits the system-wide identity change to the
/// window when wheel emulation is intentionally active.
/// </para>
/// <para>
/// A sentinel file is written when the spoof is applied and deleted on restore, allowing
/// auto-recovery if the application exits without calling <see cref="RestoreAsync"/>.
/// </para>
/// </remarks>
public interface IEmuWheelDeviceManager : IDisposable
{
    /// <summary>Gets whether the EmuWheel backend is available (vJoy installed and spoof mechanism ready).</summary>
    bool IsAvailable { get; }

    /// <summary>Gets whether a USB identity spoof is currently active.</summary>
    bool IsSpoofActive { get; }

    /// <summary>Gets the wheel model currently being spoofed, or <c>null</c> if no spoof is active.</summary>
    WheelModel? ActiveModel { get; }

    /// <summary>
    /// Applies the USB identity spoof for the specified wheel model on the given vJoy device slot,
    /// then reinitializes the device so the OS re-enumerates it with the new identity.
    /// Idempotent: calling again with the same model and slot is a no-op.
    /// </summary>
    /// <param name="model">The wheel model whose VID/PID to write to the registry.</param>
    /// <param name="vjoyId">The 1-based vJoy device slot to spoof.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplySpoofAsync(WheelModel model, uint vjoyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the original vJoy USB identity by reverting the registry to its saved values,
    /// then reinitializes the device. Safe to call even when no spoof is active.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires the specified vJoy device slot as an <see cref="IEmuWheelDevice"/> for exclusive use.
    /// Must be called after <see cref="ApplySpoofAsync"/> for the device to present with wheel identity.
    /// Idempotent: if already acquired, returns the existing instance.
    /// </summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    /// <returns>The acquired wheel device.</returns>
    /// <exception cref="Exceptions.EmuWheelException">Thrown if EmuWheel is unavailable or acquisition fails.</exception>
    IEmuWheelDevice AcquireDevice(uint vjoyId);

    /// <summary>Releases the specified device, making it available to other processes.</summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    void ReleaseDevice(uint vjoyId);

    /// <summary>Releases all currently acquired EmuWheel devices.</summary>
    void ReleaseAll();

    /// <summary>
    /// Returns a previously acquired EmuWheel device by vJoy slot ID.
    /// </summary>
    /// <param name="vjoyId">1-based vJoy device identifier.</param>
    /// <returns>The acquired <see cref="IEmuWheelDevice"/>.</returns>
    /// <exception cref="Exceptions.EmuWheelException">Thrown if the device has not been acquired.</exception>
    IEmuWheelDevice GetDevice(uint vjoyId);

    /// <summary>
    /// Checks whether a sentinel file exists from a previous session that may have
    /// exited without restoring the vJoy registry. If found, attempts to restore the
    /// original identity before the UI loads.
    /// Call once on application startup before <see cref="ApplySpoofAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecoverIfNeededAsync(CancellationToken cancellationToken = default);
}
