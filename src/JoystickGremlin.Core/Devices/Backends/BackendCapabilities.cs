// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices.Backends;

/// <summary>
/// Static capabilities of a virtual-device backend (shape of devices the backend
/// can produce, plus optional features such as FFB and identity spoofing).
/// </summary>
/// <param name="MaxDevices">Maximum number of devices the backend can host concurrently.</param>
/// <param name="MaxAxes">Maximum axis count per device.</param>
/// <param name="MaxButtons">Maximum button count per device.</param>
/// <param name="MaxHats">Maximum hat/POV count per device.</param>
/// <param name="SupportsForceFeedback">True when the backend can deliver FFB packets.</param>
/// <param name="SupportsIdentitySpoofing">True when the backend can present a custom
/// USB VID/PID and HID descriptor (e.g. spoof a Logitech G29).</param>
public sealed record BackendCapabilities(
    int MaxDevices,
    int MaxAxes,
    int MaxButtons,
    int MaxHats,
    bool SupportsForceFeedback,
    bool SupportsIdentitySpoofing);
