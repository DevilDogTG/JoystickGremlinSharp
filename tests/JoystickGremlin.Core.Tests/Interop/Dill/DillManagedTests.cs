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

    private static NativeDeviceSummary BuildDeviceSummary(
        Guid guid, string name, uint vendorId, uint productId,
        uint axisCount, uint buttonCount, uint hatCount)
    {
        return new NativeDeviceSummary
        {
            DeviceGuid = DillGuidConverter.FromGuid(guid),
            Name = name,
            VendorId = vendorId,
            ProductId = productId,
            AxisCount = axisCount,
            ButtonCount = buttonCount,
            HatCount = hatCount,
            AxisMap = new NativeAxisMap[8]
        };
    }
}
