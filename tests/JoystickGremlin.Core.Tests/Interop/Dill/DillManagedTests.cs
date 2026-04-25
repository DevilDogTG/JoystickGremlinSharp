// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Interop.Dill;

namespace JoystickGremlin.Core.Tests.Interop.Dill;

/// <summary>
/// Tests managed-layer DILL type conversions that do not require the native dill.dll to be loaded.
/// </summary>
public class DillManagedTests
{
    [Fact]
    public void DillGuidConverter_RoundTrip_PreservesValue()
    {
        Guid original = Guid.NewGuid();

        NativeGuid native = DillGuidConverter.FromGuid(original);
        Guid roundTripped = DillGuidConverter.ToGuid(native);

        roundTripped.Should().Be(original);
    }

    [Fact]
    public void DillGuidConverter_KnownGuid_ConvertsCorrectly()
    {
        // GUID_SysKeyboard: {6F1D2B61-D5A0-11CF-BFC7-444553540000}
        var known = new Guid("6F1D2B61-D5A0-11CF-BFC7-444553540000");
        NativeGuid native = DillGuidConverter.FromGuid(known);

        native.Data1.Should().Be(0x6F1D2B61u);
        native.Data2.Should().Be(0xD5A0);
        native.Data3.Should().Be(0x11CF);
        native.Data4[0].Should().Be(0xBF);
        native.Data4[1].Should().Be(0xC7);
    }

    [Fact]
    public void DillDevice_Construction_PopulatesProperties()
    {
        var guid = Guid.NewGuid();
        var native = BuildDeviceSummary(guid, "Test Device", vendorId: 0xAAAA, productId: 0xBBBB,
            axisCount: 3, buttonCount: 10, hatCount: 1);

        var device = new DillDevice(native);

        device.Guid.Should().Be(guid);
        device.Name.Should().Be("Test Device");
        device.AxisCount.Should().Be(3);
        device.ButtonCount.Should().Be(10);
        device.HatCount.Should().Be(1);
        device.VendorId.Should().Be(0xAAAAu);
        device.ProductId.Should().Be(0xBBBBu);
        device.IsVirtual.Should().BeFalse();
    }

    [Fact]
    public void DillDevice_IsVirtual_TrueForVJoyDevice()
    {
        var native = BuildDeviceSummary(Guid.NewGuid(), "vJoy Device",
            vendorId: 0x1234, productId: 0xBEAD,
            axisCount: 1, buttonCount: 1, hatCount: 0);

        var device = new DillDevice(native);

        device.IsVirtual.Should().BeTrue();
    }

    /// <summary>
    /// Regression test: button-only devices (e.g. ODDOR-GEAR) sometimes have DILL report
    /// AxisCount = 1 because DirectInput enumerates a phantom axis with HID usage = 0
    /// (no valid axis type). The managed layer must filter these out so the app matches
    /// what Windows Game Controllers shows.
    /// </summary>
    [Fact]
    public void DillDevice_AxisCount_PhantomAxisWithZeroUsageIsFiltered()
    {
        var native = new NativeDeviceSummary
        {
            DeviceGuid  = DillGuidConverter.FromGuid(Guid.NewGuid()),
            Name        = "ODDOR-GEAR",
            VendorId    = 0x1234,
            ProductId   = 0x5678,
            AxisCount   = 1,   // DILL reports 1, but it's a phantom
            ButtonCount = 8,
            HatCount    = 0,
            AxisMap     = new NativeAxisMap[8], // all zero → AxisIndex = 0 → invalid
        };

        var device = new DillDevice(native);

        device.AxisCount.Should().Be(0, "phantom axes with HID usage 0 must be filtered out");
        device.ButtonCount.Should().Be(8);
    }

    [Fact]
    public void DillDevice_AxisCount_OnlyValidUsageRangeIsCounted()
    {
        // Axis map with: one valid (X=0x30), one invalid (0x00), one valid (Y=0x31)
        var axisMap = new NativeAxisMap[8];
        axisMap[0] = new NativeAxisMap { LinearIndex = 0, AxisIndex = 0x30 }; // X — valid
        axisMap[1] = new NativeAxisMap { LinearIndex = 1, AxisIndex = 0x00 }; // phantom — invalid
        axisMap[2] = new NativeAxisMap { LinearIndex = 2, AxisIndex = 0x31 }; // Y — valid

        var native = new NativeDeviceSummary
        {
            DeviceGuid  = DillGuidConverter.FromGuid(Guid.NewGuid()),
            Name        = "Mixed Device",
            AxisCount   = 3, // DILL says 3, but only 2 are real
            ButtonCount = 4,
            HatCount    = 0,
            AxisMap     = axisMap,
        };

        var device = new DillDevice(native);

        device.AxisCount.Should().Be(2, "only entries with valid HID usage identifiers should be counted");
    }

    private static NativeDeviceSummary BuildDeviceSummary(
        Guid guid, string name, uint vendorId, uint productId,
        uint axisCount, uint buttonCount, uint hatCount)
    {
        // Populate axis map with valid HID usage identifiers (0x30=X, 0x31=Y, …)
        // so that the phantom-axis filter in DillDevice keeps them all.
        var axisMap = new NativeAxisMap[8];
        for (uint i = 0; i < Math.Min(axisCount, 8u); i++)
            axisMap[i] = new NativeAxisMap { LinearIndex = i, AxisIndex = 0x30 + i };

        return new NativeDeviceSummary
        {
            DeviceGuid  = DillGuidConverter.FromGuid(guid),
            Name        = name,
            VendorId    = vendorId,
            ProductId   = productId,
            AxisCount   = axisCount,
            ButtonCount = buttonCount,
            HatCount    = hatCount,
            AxisMap     = axisMap,
        };
    }
}
