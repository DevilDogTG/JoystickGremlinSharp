// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions.Keyboard;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class MapToKeyboardActionTests
{
    private static InputEvent ButtonPress()   => new(InputType.JoystickButton, Guid.Empty, 1, 1.0);
    private static InputEvent ButtonRelease() => new(InputType.JoystickButton, Guid.Empty, 1, 0.0);

    // ── Descriptor metadata ─────────────────────────────────────────────────

    [Fact]
    public void Descriptor_HasCorrectTagAndName()
    {
        var descriptor = MakeDescriptor(out _);

        descriptor.Tag.Should().Be(MapToKeyboardActionDescriptor.ActionTag);
        descriptor.Name.Should().Be("Map to Keyboard");
    }

    // ── Hold behavior ───────────────────────────────────────────────────────

    [Fact]
    public async Task Hold_ButtonPress_PressesBothKeysInOrder()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var order      = new List<string>();
        keyboard.Setup(k => k.KeyDown(It.IsAny<string>())).Callback<string>(k => order.Add($"down:{k}"));
        keyboard.Setup(k => k.KeyUp(It.IsAny<string>())).Callback<string>(k   => order.Add($"up:{k}"));

        var config  = new JsonObject { ["keys"] = "LShift,A", ["behavior"] = "Hold" };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonPress());

        order.Should().Equal("down:LShift", "down:A");
    }

    [Fact]
    public async Task Hold_ButtonRelease_ReleasesKeysInReverseOrder()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var order      = new List<string>();
        keyboard.Setup(k => k.KeyDown(It.IsAny<string>())).Callback<string>(k => order.Add($"down:{k}"));
        keyboard.Setup(k => k.KeyUp(It.IsAny<string>())).Callback<string>(k   => order.Add($"up:{k}"));

        var config  = new JsonObject { ["keys"] = "LShift,A", ["behavior"] = "Hold" };
        var functor = descriptor.CreateFunctor(config);

        // Full hold cycle: press then release.
        await functor.ExecuteAsync(ButtonPress());
        await functor.ExecuteAsync(ButtonRelease());

        order.Should().Equal("down:LShift", "down:A", "up:A", "up:LShift");
    }

    [Fact]
    public async Task Hold_DefaultBehavior_IsHold()
    {
        // No behavior key in config → defaults to Hold.
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "Space" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonPress());

        keyboard.Verify(k => k.KeyDown("Space"), Times.Once);
        keyboard.Verify(k => k.KeyUp(It.IsAny<string>()), Times.Never);
    }

    // ── Toggle behavior ─────────────────────────────────────────────────────

    [Fact]
    public async Task Toggle_FirstPress_PressesKeys()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "A", ["behavior"] = "Toggle" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonPress());

        keyboard.Verify(k => k.KeyDown("A"), Times.Once);
        keyboard.Verify(k => k.KeyUp(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Toggle_SecondPress_ReleasesKeys()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "A", ["behavior"] = "Toggle" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonPress()); // toggles on
        await functor.ExecuteAsync(ButtonRelease()); // release ignored by Toggle
        await functor.ExecuteAsync(ButtonPress()); // toggles off

        keyboard.Verify(k => k.KeyDown("A"), Times.Once);
        keyboard.Verify(k => k.KeyUp("A"), Times.Once);
    }

    [Fact]
    public async Task Toggle_ReleaseEvent_IsIgnored()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "A", ["behavior"] = "Toggle" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonRelease()); // should do nothing

        keyboard.VerifyNoOtherCalls();
    }

    // ── PressOnly behavior ──────────────────────────────────────────────────

    [Fact]
    public async Task PressOnly_OnPress_PressesThenImmediatelyReleasesKeys()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var order      = new List<string>();
        keyboard.Setup(k => k.KeyDown(It.IsAny<string>())).Callback<string>(k => order.Add($"down:{k}"));
        keyboard.Setup(k => k.KeyUp(It.IsAny<string>())).Callback<string>(k   => order.Add($"up:{k}"));

        var config  = new JsonObject { ["keys"] = "LCtrl,V", ["behavior"] = "PressOnly" };
        var functor = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonPress());

        order.Should().Equal("down:LCtrl", "down:V", "up:V", "up:LCtrl");
    }

    [Fact]
    public async Task PressOnly_OnRelease_DoesNothing()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "A", ["behavior"] = "PressOnly" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonRelease());

        keyboard.VerifyNoOtherCalls();
    }

    // ── ReleaseOnly behavior ────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseOnly_OnRelease_PressesThenImmediatelyReleasesKeys()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "A", ["behavior"] = "ReleaseOnly" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonRelease());

        keyboard.Verify(k => k.KeyDown("A"), Times.Once);
        keyboard.Verify(k => k.KeyUp("A"), Times.Once);
    }

    [Fact]
    public async Task ReleaseOnly_OnPress_DoesNothing()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "A", ["behavior"] = "ReleaseOnly" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonPress());

        keyboard.VerifyNoOtherCalls();
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyKeys_DoesNothing()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "", ["behavior"] = "Hold" };
        var functor    = descriptor.CreateFunctor(config);

        await functor.ExecuteAsync(ButtonPress());
        await functor.ExecuteAsync(ButtonRelease());

        keyboard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NullConfig_DoesNothing()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var functor    = descriptor.CreateFunctor(null);

        await functor.ExecuteAsync(ButtonPress());

        keyboard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnknownBehavior_FallsBackToHold()
    {
        var descriptor = MakeDescriptor(out var keyboard);
        var config     = new JsonObject { ["keys"] = "A", ["behavior"] = "NotARealBehavior" };
        var functor    = descriptor.CreateFunctor(config);

        // Hold: press → KeyDown only, release → KeyUp only.
        await functor.ExecuteAsync(ButtonPress());
        keyboard.Verify(k => k.KeyDown("A"), Times.Once);
        keyboard.Verify(k => k.KeyUp(It.IsAny<string>()), Times.Never);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MapToKeyboardActionDescriptor MakeDescriptor(out Mock<IKeyboardSimulator> keyboardMock)
    {
        keyboardMock = new Mock<IKeyboardSimulator>();
        return new MapToKeyboardActionDescriptor(
            keyboardMock.Object,
            NullLogger<MapToKeyboardActionDescriptor>.Instance);
    }
}
