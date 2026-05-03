// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Abstract base record for all force feedback commands.
/// Each command carries the effect block index that identifies which effect slot it targets.
/// </summary>
/// <param name="EffectBlockIndex">The effect block index (1-based slot identifier). Use 0 for device-level commands.</param>
public abstract record FfbCommand(byte EffectBlockIndex);

/// <summary>
/// Sets the effect type, timing, and direction for a given effect block index.
/// </summary>
/// <param name="EffectBlockIndex">The effect block index.</param>
/// <param name="EffectType">The type of force feedback effect.</param>
/// <param name="DurationMs">Duration of the effect in milliseconds.</param>
/// <param name="TriggerRepeatMs">Trigger repeat interval in milliseconds.</param>
/// <param name="SamplePeriodMs">Sample period in milliseconds.</param>
/// <param name="StartDelayMs">Start delay in milliseconds.</param>
/// <param name="Gain">Effect gain (0–255).</param>
/// <param name="TriggerButton">Trigger button index.</param>
/// <param name="AxesEnabled">Whether axes are enabled for this effect.</param>
/// <param name="IsPolar">Whether the direction is expressed as a polar angle.</param>
/// <param name="DirectionOrX">Direction in polar mode, or X component in Cartesian mode.</param>
/// <param name="DirY">Y direction component (Cartesian mode only).</param>
public sealed record SetEffectReportCommand(
    byte EffectBlockIndex,
    FfbEffectType EffectType,
    ushort DurationMs,
    ushort TriggerRepeatMs,
    ushort SamplePeriodMs,
    ushort StartDelayMs,
    byte Gain,
    byte TriggerButton,
    bool AxesEnabled,
    bool IsPolar,
    ushort DirectionOrX,
    ushort DirY) : FfbCommand(EffectBlockIndex);

/// <summary>
/// Sets the magnitude for a constant force effect.
/// </summary>
/// <param name="EffectBlockIndex">The effect block index.</param>
/// <param name="Magnitude">Force magnitude in the range -10000 to +10000.</param>
public sealed record SetConstantForceCommand(
    byte EffectBlockIndex,
    int Magnitude) : FfbCommand(EffectBlockIndex);

/// <summary>
/// Sets the start and end magnitudes for a ramp force effect.
/// </summary>
/// <param name="EffectBlockIndex">The effect block index.</param>
/// <param name="StartMagnitude">Starting force magnitude (-10000 to +10000).</param>
/// <param name="EndMagnitude">Ending force magnitude (-10000 to +10000).</param>
public sealed record SetRampForceCommand(
    byte EffectBlockIndex,
    int StartMagnitude,
    int EndMagnitude) : FfbCommand(EffectBlockIndex);

/// <summary>
/// Sets parameters for a periodic force effect (sine, square, triangle, sawtooth).
/// </summary>
/// <param name="EffectBlockIndex">The effect block index.</param>
/// <param name="Magnitude">Peak magnitude (0–10000).</param>
/// <param name="Offset">Bias offset applied to the effect.</param>
/// <param name="Phase">Phase offset in hundredths of a degree (0–35999).</param>
/// <param name="Period">Period of the waveform in microseconds.</param>
public sealed record SetPeriodicCommand(
    byte EffectBlockIndex,
    uint Magnitude,
    int Offset,
    uint Phase,
    uint Period) : FfbCommand(EffectBlockIndex);

/// <summary>
/// Sets the parameters for a condition-based effect (spring, damper, inertia, friction).
/// </summary>
/// <param name="EffectBlockIndex">The effect block index.</param>
/// <param name="IsY">If <c>true</c>, this condition applies to the Y axis; otherwise X axis.</param>
/// <param name="CenterOffset">Center point offset.</param>
/// <param name="PosCoeff">Positive coefficient.</param>
/// <param name="NegCoeff">Negative coefficient.</param>
/// <param name="PosSatur">Positive saturation value.</param>
/// <param name="NegSatur">Negative saturation value.</param>
/// <param name="DeadBand">Dead band around the center point.</param>
public sealed record SetConditionCommand(
    byte EffectBlockIndex,
    bool IsY,
    int CenterOffset,
    int PosCoeff,
    int NegCoeff,
    uint PosSatur,
    uint NegSatur,
    int DeadBand) : FfbCommand(EffectBlockIndex);

/// <summary>
/// Sets the attack and fade envelope parameters for an effect.
/// </summary>
/// <param name="EffectBlockIndex">The effect block index.</param>
/// <param name="AttackLevel">Amplitude at the start of the attack phase.</param>
/// <param name="FadeLevel">Amplitude at the end of the fade phase.</param>
/// <param name="AttackTimeMs">Duration of the attack phase in milliseconds.</param>
/// <param name="FadeTimeMs">Duration of the fade phase in milliseconds.</param>
public sealed record SetEnvelopeCommand(
    byte EffectBlockIndex,
    uint AttackLevel,
    uint FadeLevel,
    uint AttackTimeMs,
    uint FadeTimeMs) : FfbCommand(EffectBlockIndex);

/// <summary>
/// Starts, stops, or otherwise operates on a specific effect slot.
/// </summary>
/// <param name="EffectBlockIndex">The effect block index.</param>
/// <param name="Operation">The operation to perform.</param>
/// <param name="LoopCount">Number of times to loop the effect (255 = infinite).</param>
public sealed record EffectOperationCommand(
    byte EffectBlockIndex,
    FfbOperation Operation,
    byte LoopCount) : FfbCommand(EffectBlockIndex);

/// <summary>
/// Sends a device-level control command (stop all, reset, pause, etc.).
/// </summary>
/// <param name="Control">The device control command to send.</param>
public sealed record DeviceControlCommand(
    FfbDeviceCommand Control) : FfbCommand(0);

/// <summary>
/// Sets the global output gain for the device.
/// </summary>
/// <param name="Gain">Global gain value (0–255).</param>
public sealed record DeviceGainCommand(
    byte Gain) : FfbCommand(0);
