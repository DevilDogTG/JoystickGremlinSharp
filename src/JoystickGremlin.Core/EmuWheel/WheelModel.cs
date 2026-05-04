// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.EmuWheel;

/// <summary>
/// Identifies a supported wheel model whose USB identity can be spoofed by the EmuWheel backend.
/// </summary>
public enum WheelModel
{
    /// <summary>Logitech G29 (VID 0x046D, PID 0xC24F).</summary>
    LogitechG29,

    /// <summary>Logitech G920 (VID 0x046D, PID 0xC262).</summary>
    LogitechG920,

    /// <summary>Thrustmaster T300RS (VID 0x044F, PID 0xB66E).</summary>
    ThrustmasterT300RS,

    /// <summary>Thrustmaster TMX (VID 0x044F, PID 0xB36D).</summary>
    ThrustmasterTMX,
}

/// <summary>
/// Immutable metadata for a supported wheel model, including USB identity for spoofing.
/// </summary>
/// <param name="Model">The wheel model enum value.</param>
/// <param name="DisplayName">Human-readable model name shown in the UI.</param>
/// <param name="VendorId">USB Vendor ID (VID) of the wheel.</param>
/// <param name="ProductId">USB Product ID (PID) of the wheel.</param>
public sealed record WheelModelInfo(
    WheelModel Model,
    string DisplayName,
    ushort VendorId,
    ushort ProductId);

/// <summary>
/// Provides the catalogue of supported wheel models with their USB identifiers.
/// </summary>
public static class WheelModelRegistry
{
    private static readonly IReadOnlyDictionary<WheelModel, WheelModelInfo> _models =
        new Dictionary<WheelModel, WheelModelInfo>
        {
            [WheelModel.LogitechG29]        = new(WheelModel.LogitechG29,        "Logitech G29",        0x046D, 0xC24F),
            [WheelModel.LogitechG920]       = new(WheelModel.LogitechG920,       "Logitech G920",       0x046D, 0xC262),
            [WheelModel.ThrustmasterT300RS] = new(WheelModel.ThrustmasterT300RS, "Thrustmaster T300RS", 0x044F, 0xB66E),
            [WheelModel.ThrustmasterTMX]    = new(WheelModel.ThrustmasterTMX,    "Thrustmaster TMX",    0x044F, 0xB36D),
        };

    /// <summary>Gets all registered wheel models in display order.</summary>
    public static IReadOnlyList<WheelModelInfo> AllModels { get; } =
        _models.Values.OrderBy(m => m.DisplayName).ToList();

    /// <summary>Gets the metadata for the specified wheel model.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the model is not registered.</exception>
    public static WheelModelInfo Get(WheelModel model) => _models[model];

    /// <summary>Attempts to get the metadata for the specified wheel model.</summary>
    public static bool TryGet(WheelModel model, out WheelModelInfo info) =>
        _models.TryGetValue(model, out info!);
}
