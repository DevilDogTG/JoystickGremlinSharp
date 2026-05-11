// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices.Backends;

/// <summary>
/// Lifecycle / installation state of a virtual-device backend.
/// </summary>
public enum BackendStatus
{
    /// <summary>The backend's driver is not installed at all.</summary>
    NotInstalled,

    /// <summary>The driver is installed but an incompatible version is detected.</summary>
    Incompatible,

    /// <summary>The driver is installed but Windows test-signing mode must be enabled
    /// before it will load.</summary>
    NeedsTestSigning,

    /// <summary>The backend is installed and ready to acquire devices.</summary>
    Ready,

    /// <summary>The backend is installed but the last operation reported an error;
    /// see logs for details.</summary>
    Error,
}
