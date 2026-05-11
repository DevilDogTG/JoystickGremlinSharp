// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices.Backends;

/// <summary>
/// Categorises a virtual-device backend so the UI and profile system can pick
/// the right backend for a given use case (generic controller versus racing wheel).
/// </summary>
public enum BackendKind
{
    /// <summary>Generic gamepad / joystick (e.g. stock vJoy).</summary>
    GenericController,

    /// <summary>Wheel-shaped HID device with steering / pedal usages and FFB
    /// (e.g. the upcoming JGS Wheel fork of vJoy).</summary>
    RacingWheel,
}
