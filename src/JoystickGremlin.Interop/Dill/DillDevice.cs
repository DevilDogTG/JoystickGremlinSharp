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

    internal DillDevice(NativeDeviceSummary native)
    {
        Guid = DillGuidConverter.ToGuid(native.DeviceGuid);
        Name = native.Name ?? string.Empty;
        ButtonCount = (int)native.ButtonCount;
        HatCount = (int)native.HatCount;
        VendorId = native.VendorId;
        ProductId = native.ProductId;

        var mappings = new List<AxisMapping>();
        var axisMap = native.AxisMap ?? [];

        // DILL sometimes reports AxisCount = 0 even when axes exist. Walk the AxisMap
        // directly and stop at the first entry where AxisIndex = 0 (DirectInput axis codes
        // start at 0x30, so 0 is the sentinel for an unused slot).
        var maxEntries = native.AxisCount > 0 ? (int)native.AxisCount : axisMap.Length;
        for (int i = 0; i < maxEntries && i < axisMap.Length; i++)
        {
            if (axisMap[i].AxisIndex == 0) break;
            mappings.Add(new AxisMapping(axisMap[i].LinearIndex, axisMap[i].AxisIndex));
        }
        AxisMappings = mappings;

        // Prefer the DILL-reported count but fall back to AxisMap-derived count when DILL
        // reports 0 (observed on some devices, e.g. MOZA R9 Base).
        AxisCount = native.AxisCount > 0 ? (int)native.AxisCount : mappings.Count;
    }
}

/// <summary>Represents one axis mapping entry: linear index ↔ DirectInput axis identifier.</summary>
/// <param name="LinearIndex">Zero-based sequential axis index.</param>
/// <param name="AxisIndex">DirectInput axis identifier.</param>
internal sealed record AxisMapping(uint LinearIndex, uint AxisIndex);
