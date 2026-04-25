// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions.ChangeMode;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Modes;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class ChangeModeActionTests
{
    private static InputEvent MakeButton(double value) =>
        new(InputType.JoystickButton, Guid.Empty, 1, value, "Default");

    // ── Descriptor metadata ─────────────────────────────────────────────────

    [Fact]
    public void Descriptor_HasCorrectTagAndName()
    {
        var (descriptor, _) = MakeDescriptor();

        descriptor.Tag.Should().Be(ChangeModeActionDescriptor.ActionTag);
        descriptor.Name.Should().Be("Change Mode");
    }

    // ── Mode switching ──────────────────────────────────────────────────────

    [Fact]
    public async Task Functor_ButtonPress_SwitchesToTargetMode()
    {
        var (descriptor, modeManager) = MakeDescriptor();
        var config = new JsonObject { ["targetMode"] = "Combat" };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(MakeButton(1.0));

        modeManager.Verify(m => m.SwitchTo("Combat"), Times.Once);
    }

    [Fact]
    public async Task Functor_ButtonRelease_DoesNotSwitch()
    {
        var (descriptor, modeManager) = MakeDescriptor();
        var config = new JsonObject { ["targetMode"] = "Combat" };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(MakeButton(0.0));

        modeManager.Verify(m => m.SwitchTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Functor_EmptyTargetMode_DoesNotSwitch()
    {
        var (descriptor, modeManager) = MakeDescriptor();
        var config = new JsonObject { ["targetMode"] = "" };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(MakeButton(1.0));

        modeManager.Verify(m => m.SwitchTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Functor_NullConfig_DoesNotSwitch()
    {
        var (descriptor, modeManager) = MakeDescriptor();
        var functor = descriptor.CreateFunctor(null);

        await functor.ExecuteAsync(MakeButton(1.0));

        modeManager.Verify(m => m.SwitchTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Functor_SwitchToThrows_DoesNotPropagateException()
    {
        var (descriptor, modeManager) = MakeDescriptor();
        modeManager.Setup(m => m.SwitchTo(It.IsAny<string>()))
            .Throws(new Exceptions.ModeException("Mode not found"));

        var config = new JsonObject { ["targetMode"] = "NonExistent" };
        var functor = descriptor.CreateFunctor(config);

        // Should not throw
        var act = async () => await functor.ExecuteAsync(MakeButton(1.0));
        await act.Should().NotThrowAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (ChangeModeActionDescriptor descriptor, Mock<IModeManager> modeManager) MakeDescriptor()
    {
        var modeManager = new Mock<IModeManager>();
        var descriptor = new ChangeModeActionDescriptor(
            modeManager.Object,
            NullLogger<ChangeModeActionDescriptor>.Instance);
        return (descriptor, modeManager);
    }
}
