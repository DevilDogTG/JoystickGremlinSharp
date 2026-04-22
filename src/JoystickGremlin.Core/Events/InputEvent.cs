// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;

namespace JoystickGremlin.Core.Events;

/// <summary>
/// Represents a single input event captured from a physical device or keyboard.
/// </summary>
/// <param name="InputType">The type of input that generated this event.</param>
/// <param name="DeviceGuid">The GUID of the originating device.</param>
/// <param name="Identifier">The axis/button/hat index (or keyboard scan code).</param>
/// <param name="Value">The input value (axis: -1.0–1.0; button: 0.0/1.0; hat: degrees or -1).</param>
/// <param name="Mode">The active mode name when the event was captured.</param>
public sealed record InputEvent(
    InputType InputType,
    Guid DeviceGuid,
    int Identifier,
    double Value,
    string Mode
);
