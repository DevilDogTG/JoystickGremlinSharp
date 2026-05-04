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
        new(InputType.JoystickButton, Guid.Empty, 1, value);

    private static InputEvent MakeEvent(InputType inputType, double value) =>
        new(inputType, Guid.Empty, 1, value);

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

    [Fact]
    public async Task ButtonFunctor_NullConfig_UsesDefaultThreshold()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var descriptor = new VJoyButtonDescriptor(manager.Object, NullLogger<VJoyButtonDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(null);

        // Exactly at default threshold (0.5) → pressed
        await functor.ExecuteAsync(MakeEvent(0.5));
        // Below default threshold → released
        await functor.ExecuteAsync(MakeEvent(0.49));

        device.Verify(d => d.SetButton(1, true), Times.Once);
        device.Verify(d => d.SetButton(1, false), Times.Once);
    }

    [Fact]
    public async Task ButtonFunctor_CustomThreshold_FiresAtConfiguredLevel()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        // Hair-trigger: fires at 10% travel
        var config = new JsonObject { ["vjoyId"] = 1, ["buttonIndex"] = 2, ["threshold"] = 0.1 };
        var descriptor = new VJoyButtonDescriptor(manager.Object, NullLogger<VJoyButtonDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(config);

        // 0.1 → pressed (at threshold)
        await functor.ExecuteAsync(MakeEvent(InputType.JoystickAxis, 0.1));
        // 0.05 → released (below threshold)
        await functor.ExecuteAsync(MakeEvent(InputType.JoystickAxis, 0.05));

        device.Verify(d => d.SetButton(2, true), Times.Once);
        device.Verify(d => d.SetButton(2, false), Times.Once);
    }

    [Fact]
    public async Task ButtonFunctor_CustomThreshold_DoesNotAffectDirectButtonMappings()
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var config = new JsonObject { ["vjoyId"] = 1, ["buttonIndex"] = 2, ["threshold"] = 0.9 };
        var descriptor = new VJoyButtonDescriptor(manager.Object, NullLogger<VJoyButtonDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(MakeEvent(InputType.JoystickButton, 1.0));
        await functor.ExecuteAsync(MakeEvent(InputType.JoystickButton, 0.0));

        device.Verify(d => d.SetButton(2, true), Times.Once);
        device.Verify(d => d.SetButton(2, false), Times.Once);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]   // out-of-range low → clamped to 0.0
    [InlineData(1.5, 1.0)]    // out-of-range high → clamped to 1.0
    public async Task ButtonFunctor_ThresholdClamped_NeverThrows(double rawThreshold, double expectedClamped)
    {
        var device = new Mock<IVirtualDevice>();
        var manager = MockManagerWith(device, vjoyId: 1u);

        var config = new JsonObject { ["vjoyId"] = 1, ["buttonIndex"] = 1, ["threshold"] = rawThreshold };
        var descriptor = new VJoyButtonDescriptor(manager.Object, NullLogger<VJoyButtonDescriptor>.Instance);
        var functor = descriptor.CreateFunctor(config);

        // Value at clamped threshold → pressed; one tick below → released
        await functor.ExecuteAsync(MakeEvent(InputType.JoystickAxis, expectedClamped));
        device.Verify(d => d.SetButton(1, true), Times.Once);
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
