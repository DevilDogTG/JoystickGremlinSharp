// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Devices.Backends;
using Moq;

namespace JoystickGremlin.Core.Tests.Devices.Backends;

public sealed class BackendRegistryTests
{
    private static IVirtualDeviceBackend MakeBackend(string id, BackendKind kind = BackendKind.GenericController)
    {
        var mock = new Mock<IVirtualDeviceBackend>(MockBehavior.Strict);
        mock.SetupGet(b => b.Id).Returns(id);
        mock.SetupGet(b => b.Kind).Returns(kind);
        return mock.Object;
    }

    [Fact]
    public void Backends_ReturnsItemsInRegistrationOrder()
    {
        var first = MakeBackend("vjoy");
        var second = MakeBackend("jgs-wheel", BackendKind.RacingWheel);

        var sut = new BackendRegistry([first, second]);

        sut.Backends.Should().Equal(first, second);
    }

    [Fact]
    public void DefaultBackendId_IsFirstRegistered()
    {
        var sut = new BackendRegistry([MakeBackend("vjoy"), MakeBackend("jgs-wheel")]);

        sut.DefaultBackendId.Should().Be("vjoy");
    }

    [Fact]
    public void DefaultBackendId_NoBackends_Throws()
    {
        var sut = new BackendRegistry([]);

        var act = () => sut.DefaultBackendId;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Find_KnownId_ReturnsBackend()
    {
        var wheel = MakeBackend("jgs-wheel", BackendKind.RacingWheel);
        var sut = new BackendRegistry([MakeBackend("vjoy"), wheel]);

        sut.Find("jgs-wheel").Should().BeSameAs(wheel);
    }

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        var sut = new BackendRegistry([MakeBackend("vjoy")]);

        sut.Find("missing").Should().BeNull();
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        var wheel = MakeBackend("JGS-Wheel", BackendKind.RacingWheel);
        var sut = new BackendRegistry([wheel]);

        sut.Find("jgs-wheel").Should().BeSameAs(wheel);
    }

    [Fact]
    public void Resolve_NullId_ReturnsDefault()
    {
        var def = MakeBackend("vjoy");
        var sut = new BackendRegistry([def, MakeBackend("jgs-wheel")]);

        sut.Resolve(null).Should().BeSameAs(def);
    }

    [Fact]
    public void Resolve_UnknownId_FallsBackToDefault()
    {
        var def = MakeBackend("vjoy");
        var sut = new BackendRegistry([def]);

        sut.Resolve("nonexistent").Should().BeSameAs(def);
    }

    [Fact]
    public void Resolve_KnownId_ReturnsThatBackend()
    {
        var wheel = MakeBackend("jgs-wheel", BackendKind.RacingWheel);
        var sut = new BackendRegistry([MakeBackend("vjoy"), wheel]);

        sut.Resolve("jgs-wheel").Should().BeSameAs(wheel);
    }

    [Fact]
    public void Constructor_DuplicateIds_Throws()
    {
        var act = () => new BackendRegistry([MakeBackend("vjoy"), MakeBackend("vjoy")]);

        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*vjoy*");
    }

    [Fact]
    public void Constructor_DuplicateIdsCaseInsensitive_Throws()
    {
        var act = () => new BackendRegistry([MakeBackend("vjoy"), MakeBackend("VJoy")]);

        act.Should().Throw<ArgumentException>();
    }
}
