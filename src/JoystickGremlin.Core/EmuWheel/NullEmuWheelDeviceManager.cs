// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Exceptions;

namespace JoystickGremlin.Core.EmuWheel;

/// <summary>
/// No-op implementation of <see cref="IEmuWheelDeviceManager"/> used when the EmuWheel backend
/// is not available (e.g., in unit tests or when the Interop layer has not registered a real implementation).
/// All device acquisition calls throw <see cref="EmuWheelException"/>.
/// </summary>
public sealed class NullEmuWheelDeviceManager : IEmuWheelDeviceManager
{
    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public bool IsSpoofActive => false;

    /// <inheritdoc/>
    public bool RebootRecommended => false;

    /// <inheritdoc/>
    public WheelModel? ActiveModel => null;

    /// <inheritdoc/>
    public Task ApplySpoofAsync(WheelModel model, uint vjoyId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task RestoreAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public IEmuWheelDevice AcquireDevice(uint vjoyId)
        => throw new EmuWheelException("EmuWheel backend is not available in this environment.");

    /// <inheritdoc/>
    public void ReleaseDevice(uint vjoyId) { }

    /// <inheritdoc/>
    public void ReleaseAll() { }

    /// <inheritdoc/>
    public IEmuWheelDevice GetDevice(uint vjoyId)
        => throw new EmuWheelException("EmuWheel backend is not available in this environment.");

    /// <inheritdoc/>
    public Task RecoverIfNeededAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public void Dispose() { }
}
