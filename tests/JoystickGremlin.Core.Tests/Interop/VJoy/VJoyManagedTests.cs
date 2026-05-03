// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Exceptions;
using JoystickGremlin.Interop.VJoy;

namespace JoystickGremlin.Core.Tests.Interop.VJoy;

/// <summary>
/// Tests for the vJoy managed layer. Uses mocked <see cref="IVirtualDevice"/> to verify
/// interface contracts without requiring the vJoy driver to be installed.
/// </summary>
public class VJoyManagedTests
{
    // ── IVirtualDevice interface contract tests ──────────────────────────────

    [Fact]
    public void MockVirtualDevice_SetAxis_AcceptsNormalisedValues()
    {
        var device = new Mock<IVirtualDevice>();
        device.Setup(d => d.SetAxis(It.IsAny<int>(), It.IsInRange(-1.0, 1.0, Moq.Range.Inclusive)));

        device.Object.SetAxis(1, 0.5);
        device.Object.SetAxis(2, -1.0);
        device.Object.SetAxis(3, 1.0);

        device.Verify(d => d.SetAxis(1, 0.5), Times.Once);
        device.Verify(d => d.SetAxis(2, -1.0), Times.Once);
        device.Verify(d => d.SetAxis(3, 1.0), Times.Once);
    }

    [Fact]
    public void MockVirtualDevice_SetButton_RecordsState()
    {
        var device = new Mock<IVirtualDevice>();

        device.Object.SetButton(1, true);
        device.Object.SetButton(1, false);

        device.Verify(d => d.SetButton(1, true), Times.Once);
        device.Verify(d => d.SetButton(1, false), Times.Once);
    }

    [Fact]
    public void MockVirtualDevice_SetHat_AcceptsCenter()
    {
        var device = new Mock<IVirtualDevice>();

        device.Object.SetHat(1, -1);

        device.Verify(d => d.SetHat(1, -1), Times.Once);
    }

    [Fact]
    public void MockVirtualDevice_Reset_IsCalled()
    {
        var device = new Mock<IVirtualDevice>();

        device.Object.Reset();

        device.Verify(d => d.Reset(), Times.Once);
    }

    // ── IVirtualDeviceManager interface contract tests ───────────────────────

    [Fact]
    public void MockVirtualDeviceManager_AcquireDevice_ReturnsDevice()
    {
        var mockDevice = new Mock<IVirtualDevice>();
        mockDevice.SetupGet(d => d.DeviceId).Returns(1u);

        var manager = new Mock<IVirtualDeviceManager>();
        manager.Setup(m => m.AcquireDevice(1u)).Returns(mockDevice.Object);

        var device = manager.Object.AcquireDevice(1u);

        device.Should().NotBeNull();
        device.DeviceId.Should().Be(1u);
    }

    [Fact]
    public void MockVirtualDeviceManager_AcquireTwice_ThrowsVJoyException()
    {
        var manager = new Mock<IVirtualDeviceManager>();
        manager.Setup(m => m.AcquireDevice(1u))
               .Throws(new VJoyException("vJoy device 1 is already acquired by this process."));

        Action act = () => manager.Object.AcquireDevice(1u);

        act.Should().Throw<VJoyException>().WithMessage("*already acquired*");
    }

    [Fact]
    public void MockVirtualDeviceManager_GetAvailableDeviceIds_ReturnsIds()
    {
        var manager = new Mock<IVirtualDeviceManager>();
        manager.Setup(m => m.GetAvailableDeviceIds()).Returns([1u, 2u, 3u]);

        var ids = manager.Object.GetAvailableDeviceIds();

        ids.Should().BeEquivalentTo([1u, 2u, 3u]);
    }

    [Fact]
    public void GetOrAcquireDevice_WhenAlreadyAcquired_ReturnsExistingDevice()
    {
        var existingDevice = new Mock<IVirtualDevice>();
        existingDevice.SetupGet(d => d.DeviceId).Returns(2u);

        var manager = new Mock<IVirtualDeviceManager>();
        manager.Setup(m => m.GetDevice(2u)).Returns(existingDevice.Object);

        var device = manager.Object.GetOrAcquireDevice(2u);

        device.Should().BeSameAs(existingDevice.Object);
        manager.Verify(m => m.GetDevice(2u), Times.Once);
        manager.Verify(m => m.AcquireDevice(It.IsAny<uint>()), Times.Never);
    }

    [Fact]
    public void GetOrAcquireDevice_WhenNotAcquired_AcquiresRequestedDevice()
    {
        var acquiredDevice = new Mock<IVirtualDevice>();
        acquiredDevice.SetupGet(d => d.DeviceId).Returns(2u);

        var manager = new Mock<IVirtualDeviceManager>();
        manager.Setup(m => m.GetDevice(2u))
            .Throws(new VJoyException("vJoy device 2 has not been acquired. Call AcquireDevice first."));
        manager.Setup(m => m.AcquireDevice(2u)).Returns(acquiredDevice.Object);

        var device = manager.Object.GetOrAcquireDevice(2u);

        device.Should().BeSameAs(acquiredDevice.Object);
        manager.Verify(m => m.GetDevice(2u), Times.Once);
        manager.Verify(m => m.AcquireDevice(2u), Times.Once);
    }

    [Fact]
    public void ForceReacquireDevice_ReleasesAndReacquires()
    {
        var freshDevice = new Mock<IVirtualDevice>();
        freshDevice.SetupGet(d => d.DeviceId).Returns(1u);

        var manager = new Mock<IVirtualDeviceManager>();
        manager.Setup(m => m.AcquireDevice(1u)).Returns(freshDevice.Object);

        var device = manager.Object.ForceReacquireDevice(1u);

        manager.Verify(m => m.ReleaseDevice(1u), Times.Once);
        manager.Verify(m => m.AcquireDevice(1u), Times.Once);
        device.Should().BeSameAs(freshDevice.Object);
    }

    [Fact]
    public void ForceReacquireDevice_NullManager_ThrowsArgumentNullException()
    {
        IVirtualDeviceManager? manager = null;
        Action act = () => manager!.ForceReacquireDevice(1u);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Axis normalisation formula tests (unit test without DLL) ────────────

    [Theory]
    [InlineData(0.0, 16384.0, 16384)]   // centre: halfRange + halfRange * 0 + 0.5 → 16384.5 → 16384
    [InlineData(1.0, 16384.0, 32768)]   // max: halfRange * 2 + 0.5 → 32768.5 → 32768 (clamped by int)
    [InlineData(-1.0, 16384.0, 0)]      // min: 0 + 0.5 → 0 (halfRange - halfRange + 0.5 = 0.5 → 0)
    public void AxisNormalisationFormula_GivesExpectedRawValue(
        double normalised, double halfRange, int expectedRaw)
    {
        int raw = (int)(halfRange + halfRange * normalised + 0.5);
        raw.Should().Be(expectedRaw);
    }

    [Fact]
    public void AxisNormalisationFormula_ClampsAboveOne()
    {
        double halfRange = 16384.0;
        double overdriven = 1.5;
        double clamped = Math.Clamp(overdriven, -1.0, 1.0);
        int raw = (int)(halfRange + halfRange * clamped + 0.5);
        raw.Should().Be(32768);
    }
}
