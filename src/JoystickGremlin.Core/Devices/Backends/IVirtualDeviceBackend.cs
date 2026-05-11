// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices.Backends;

/// <summary>
/// Abstraction over a virtual-device provider (vJoy, JGS Wheel fork, future ViGEm
/// wheel target, etc.). Wraps an <see cref="IVirtualDeviceManager"/> so existing
/// per-backend manager logic is reused; the backend layer adds backend-level
/// metadata (id, kind, capabilities, install status).
/// </summary>
public interface IVirtualDeviceBackend
{
    /// <summary>Stable identifier persisted in profiles (e.g. <c>"vjoy"</c>, <c>"jgs-wheel"</c>).</summary>
    string Id { get; }

    /// <summary>Human-readable name shown in the UI (e.g. <c>"vJoy"</c>, <c>"JGS Wheel"</c>).</summary>
    string DisplayName { get; }

    /// <summary>The category of devices this backend produces.</summary>
    BackendKind Kind { get; }

    /// <summary>Static capabilities advertised by this backend.</summary>
    BackendCapabilities Capabilities { get; }

    /// <summary>Current install / readiness state — re-evaluated each access.</summary>
    BackendStatus Status { get; }

    /// <summary>The device manager used to acquire / release virtual devices on this backend.</summary>
    IVirtualDeviceManager Manager { get; }
}
