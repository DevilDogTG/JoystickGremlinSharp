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

        // Always walk the full AxisMap to discover all valid axes, regardless of AxisCount.
        // DILL sometimes under-reports (e.g. MOZA R9 Base reports 7 but has 8 valid entries)
        // or reports 0 even when axes exist. DirectInput axis codes start at 0x30, so
        // AxisIndex == 0 is the sentinel for an unused slot.
        for (int i = 0; i < axisMap.Length; i++)
        {
            if (axisMap[i].AxisIndex == 0) break;
            mappings.Add(new AxisMapping(axisMap[i].LinearIndex, axisMap[i].AxisIndex));
        }
        AxisMappings = mappings;

        // Use the larger of: what DILL reports vs. what the AxisMap actually contains.
        // This handles under-reporting (AxisCount < real count) and zero-reporting alike.
        AxisCount = Math.Max((int)native.AxisCount, mappings.Count);
    }
}

/// <summary>Represents one axis mapping entry: linear index ↔ DirectInput axis identifier.</summary>
/// <param name="LinearIndex">Zero-based sequential axis index.</param>
/// <param name="AxisIndex">DirectInput axis identifier.</param>
internal sealed record AxisMapping(uint LinearIndex, uint AxisIndex);
