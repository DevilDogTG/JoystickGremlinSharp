// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;

namespace JoystickGremlin.Interop.Dill;

/// <summary>
/// Managed representation of a physical DirectInput device discovered by DILL.
/// Wraps the <see cref="NativeDeviceSummary"/> returned from the native library.
/// </summary>
public sealed class DillDevice : IPhysicalDevice
{
    /// <inheritdoc/>
    public Guid Guid { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public int AxisCount { get; }

    /// <inheritdoc/>
    public int ButtonCount { get; }

    /// <inheritdoc/>
    public int HatCount { get; }

    /// <summary>Gets the USB vendor identifier of the device.</summary>
    public uint VendorId { get; }

    /// <summary>Gets the USB product identifier of the device.</summary>
    public uint ProductId { get; }

    /// <summary>
    /// Gets whether this is a virtual vJoy device (vendor 0x1234, product 0xBEAD).
    /// </summary>
    public bool IsVirtual => VendorId == 0x1234 && ProductId == 0xBEAD;

    /// <summary>
    /// Gets axis mappings from this device: each entry maps a DirectInput axis index to its
    /// sequential (linear) index used for normalised access.
    /// </summary>
    internal IReadOnlyList<AxisMapping> AxisMappings { get; }

    // Valid HID Generic Desktop axis usage IDs: X(0x30) through Slider1(0x37).
    // DirectInput can report axes with AxisIndex = 0 (no valid HID usage) for button-only
    // devices whose HID descriptors contain a residual axis entry. Those phantom axes are
    // filtered here so AxisCount matches what Windows Game Controllers shows.
    private const uint MinValidAxisUsage = 0x30;
    private const uint MaxValidAxisUsage = 0x37;

    internal DillDevice(NativeDeviceSummary native)
    {
        Guid = DillGuidConverter.ToGuid(native.DeviceGuid);
        Name = native.Name ?? string.Empty;
        ButtonCount = (int)native.ButtonCount;
        HatCount = (int)native.HatCount;
        VendorId = native.VendorId;
        ProductId = native.ProductId;

        // Build axis mappings, skipping entries with an invalid DirectInput axis identifier.
        var mappings = new List<AxisMapping>((int)native.AxisCount);
        var axisMap = native.AxisMap ?? [];
        for (int i = 0; i < Math.Min(axisMap.Length, (int)native.AxisCount); i++)
        {
            var axisUsage = axisMap[i].AxisIndex;
            if (axisUsage >= MinValidAxisUsage && axisUsage <= MaxValidAxisUsage)
                mappings.Add(new AxisMapping(axisMap[i].LinearIndex, axisUsage));
        }
        AxisMappings = mappings;

        // Derive AxisCount from validated mappings, not from the raw DILL value, so that
        // phantom axes reported by DirectInput for button-only devices are not counted.
        AxisCount = AxisMappings.Count;
    }
}

/// <summary>Represents one axis mapping entry: linear index ↔ DirectInput axis identifier.</summary>
/// <param name="LinearIndex">Zero-based sequential axis index.</param>
/// <param name="AxisIndex">DirectInput axis identifier.</param>
internal sealed record AxisMapping(uint LinearIndex, uint AxisIndex);
