// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class VJoyActionTests
{
    private static InputEvent MakeEvent(double value = 0.0) =>
        new(InputType.JoystickButton, Guid.Empty, 1, value, "Default");

    // ── VJoyAxisDescriptor ─────────────────────────────────────────────────

    [Fact]
    public async Task AxisFunctor_SetsAxis_WithConfiguredIndexAndValue()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var config = new JsonObject { ["vjoyId"] = 1, ["axisIndex"] = 3 };
        var descriptor = new VJoyAxisDescriptor(manager.Object, NullLogger<VJoyAxisDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(MakeEvent(0.75));

        device.Verify(d => d.SetAxis(3, 0.75), Times.Once);
    }

    [Fact]
    public async Task AxisFunctor_NullConfig_UsesDefaults()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var descriptor = new VJoyAxisDescriptor(manager.Object, NullLogger<VJoyAxisDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(null);

        await functor.ExecuteAsync(MakeEvent(-0.5));

        device.Verify(d => d.SetAxis(1, -0.5), Times.Once);
    }

    // ── VJoyButtonDescriptor ───────────────────────────────────────────────

    [Fact]
    public async Task ButtonFunctor_ValueAtOrAbove0_5_SetsPressed()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var config = new JsonObject { ["vjoyId"] = 1, ["buttonIndex"] = 5 };
        var descriptor = new VJoyButtonDescriptor(manager.Object, NullLogger<VJoyButtonDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(MakeEvent(1.0));
        await functor.ExecuteAsync(MakeEvent(0.0));

        device.Verify(d => d.SetButton(5, true), Times.Once);
        device.Verify(d => d.SetButton(5, false), Times.Once);
    }

    // ── VJoyHatDescriptor ──────────────────────────────────────────────────

    [Fact]
    public async Task HatFunctor_SetsHat_WithDegreeValue()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var config = new JsonObject { ["vjoyId"] = 1, ["hatIndex"] = 1 };
        var descriptor = new VJoyHatDescriptor(manager.Object, NullLogger<VJoyHatDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(MakeEvent(9000)); // 90 degrees → East

        device.Verify(d => d.SetHat(1, 9000), Times.Once);
    }

    [Fact]
    public async Task HatFunctor_CenterValue_SetsMinusOne()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var descriptor = new VJoyHatDescriptor(manager.Object, NullLogger<VJoyHatDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(null);

        await functor.ExecuteAsync(MakeEvent(-1));

        device.Verify(d => d.SetHat(1, -1), Times.Once);
    }

    // ── Descriptor tag/name tests ──────────────────────────────────────────

    [Fact]
    public void AllDescriptors_HaveCorrectTags()
    {
        var mgr = new Mock<IVirtualDeviceManager>();
        IActionDescriptor[] descriptors =
        [
            new VJoyAxisDescriptor(mgr.Object, NullLogger<VJoyAxisDescriptor>.Instance),
            new VJoyButtonDescriptor(mgr.Object, NullLogger<VJoyButtonDescriptor>.Instance),
            new VJoyHatDescriptor(mgr.Object, NullLogger<VJoyHatDescriptor>.Instance),
        ];

        descriptors.Select(d => d.Tag).Should().BeEquivalentTo(
            VJoyAxisDescriptor.ActionTag,
            VJoyButtonDescriptor.ActionTag,
            VJoyHatDescriptor.ActionTag);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Mock<IVirtualDeviceManager> MockManagerWith(Mock<IVirtualDevice> device, uint vjoyId)
    {
        var manager = new Mock<IVirtualDeviceManager>();
        manager.Setup(m => m.AcquireDevice(vjoyId)).Returns(device.Object);
        return manager;
    }
}
