// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Identifies the type of force feedback effect, matching vJoy's FFBEType enumeration.
/// </summary>
public enum FfbEffectType
{
    /// <summary>No effect.</summary>
    None = 0,

    /// <summary>Constant force effect.</summary>
    ConstantForce = 1,

    /// <summary>Ramp force effect.</summary>
    Ramp = 2,

    /// <summary>Square wave periodic effect.</summary>
    Square = 3,

    /// <summary>Sine wave periodic effect.</summary>
    Sine = 4,

    /// <summary>Triangle wave periodic effect.</summary>
    Triangle = 5,

    /// <summary>Sawtooth up periodic effect.</summary>
    SawtoothUp = 6,

    /// <summary>Sawtooth down periodic effect.</summary>
    SawtoothDown = 7,

    /// <summary>Spring condition effect.</summary>
    Spring = 8,

    /// <summary>Damper condition effect.</summary>
    Damper = 9,

    /// <summary>Inertia condition effect.</summary>
    Inertia = 10,

    /// <summary>Friction condition effect.</summary>
    Friction = 11,

    /// <summary>Custom force data effect.</summary>
    Custom = 12,
}
