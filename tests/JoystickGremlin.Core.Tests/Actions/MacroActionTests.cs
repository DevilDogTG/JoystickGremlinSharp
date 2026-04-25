// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions.Keyboard;
using JoystickGremlin.Core.Actions.Macro;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class MacroActionTests
{
    private static InputEvent Press(double value = 1.0) =>
        new(InputType.JoystickButton, Guid.Empty, 1, value, "Default");

    // ── Descriptor metadata ─────────────────────────────────────────────────

    [Fact]
    public void Descriptor_HasCorrectTagAndName()
    {
        var descriptor = MakeDescriptor(out _);

        descriptor.Tag.Should().Be(MacroActionDescriptor.ActionTag);
        descriptor.Name.Should().Be("Macro");
    }

    // ── Key dispatching ─────────────────────────────────────────────────────

    [Fact]
    public async Task Functor_OnPress_PressesThenReleasesAllKeys()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var order = new List<string>();
        keyboard.Setup(k => k.KeyDown(It.IsAny<string>()))
            .Callback<string>(k => order.Add($"down:{k}"));
        keyboard.Setup(k => k.KeyUp(It.IsAny<string>()))
            .Callback<string>(k => order.Add($"up:{k}"));

        var config = new JsonObject { ["keys"] = "LControl,C", ["onPress"] = true };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(Press(1.0));

        order.Should().ContainInOrder("down:LControl", "down:C", "up:C", "up:LControl");
    }

    [Fact]
    public async Task Functor_OnPressTrue_IgnoresReleaseEvent()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config = new JsonObject { ["keys"] = "A", ["onPress"] = true };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(Press(0.0)); // button release

        keyboard.Verify(k => k.KeyDown(It.IsAny<string>()), Times.Never);
        keyboard.Verify(k => k.KeyUp(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Functor_OnPressFalse_FiresOnRelease()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config = new JsonObject { ["keys"] = "Space", ["onPress"] = false };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(Press(0.0)); // release triggers it

        keyboard.Verify(k => k.KeyDown("Space"), Times.Once);
        keyboard.Verify(k => k.KeyUp("Space"), Times.Once);
    }

    [Fact]
    public async Task Functor_EmptyKeys_DoesNothing()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config = new JsonObject { ["keys"] = "", ["onPress"] = true };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(Press(1.0));

        keyboard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Functor_NullConfig_DoesNothing()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var functor = descriptor.CreateFunctor(null);

        await functor.ExecuteAsync(Press(1.0));

        keyboard.VerifyNoOtherCalls();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MacroActionDescriptor MakeDescriptor(out Mock<IKeyboardSimulator> keyboardMock)
    {
        keyboardMock = new Mock<IKeyboardSimulator>();
        return new MacroActionDescriptor(keyboardMock.Object, NullLogger<MacroActionDescriptor>.Instance);
    }
}
