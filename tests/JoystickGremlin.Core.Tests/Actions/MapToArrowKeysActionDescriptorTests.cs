// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions.Keyboard;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Actions;

using Direction = MapToArrowKeysActionDescriptor.Direction;

public sealed class MapToArrowKeysActionDescriptorTests
{
    private static InputEvent Hat(double degrees) =>
        new(InputType.JoystickHat, Guid.Empty, 1, degrees);

    private static InputEvent Button(int id, bool pressed) =>
        new(InputType.JoystickButton, Guid.Empty, id, pressed ? 1.0 : 0.0);

    // ── Descriptor metadata ──────────────────────────────────────────────────

    [Fact]
    public void Descriptor_HasCorrectTagAndName()
    {
        var d = MakeDescriptor(out _);

        d.Tag.Should().Be(MapToArrowKeysActionDescriptor.ActionTag);
        d.Name.Should().NotBeNullOrWhiteSpace();
    }

    // ── ResolveHatDirection 8-way sector logic ───────────────────────────────

    [Theory]
    [InlineData(-1.0,    Direction.None)]
    [InlineData(0.0,     Direction.Up)]
    [InlineData(45.0,    Direction.Up   | Direction.Right)]
    [InlineData(90.0,    Direction.Right)]
    [InlineData(135.0,   Direction.Down | Direction.Right)]
    [InlineData(180.0,   Direction.Down)]
    [InlineData(225.0,   Direction.Down | Direction.Left)]
    [InlineData(270.0,   Direction.Left)]
    [InlineData(315.0,   Direction.Up   | Direction.Left)]
    [InlineData(360.0,   Direction.Up)]               // wraps
    [InlineData(22.4,    Direction.Up)]               // just under NE boundary
    [InlineData(22.5,    Direction.Up   | Direction.Right)]
    [InlineData(67.4,    Direction.Up   | Direction.Right)]
    [InlineData(67.5,    Direction.Right)]
    public void ResolveHatDirection_DegreeAngles_MapToExpectedSector(double degrees, Direction expected)
    {
        MapToArrowKeysActionDescriptor.ResolveHatDirection(degrees).Should().Be(expected);
    }

    [Fact]
    public void ResolveHatDirection_Centidegrees_NormalisesToDegrees()
    {
        // 4500 centidegrees = 45° = NE
        MapToArrowKeysActionDescriptor.ResolveHatDirection(4500)
            .Should().Be(Direction.Up | Direction.Right);

        // 18000 centidegrees = 180° = S
        MapToArrowKeysActionDescriptor.ResolveHatDirection(18000)
            .Should().Be(Direction.Down);
    }

    // ── Hat mode: end-to-end functor behavior ────────────────────────────────

    [Fact]
    public async Task Hat_DefaultConfig_UsesArrowKeysOnPress()
    {
        var d = MakeDescriptor(out var kb);
        var f = d.CreateFunctor(null); // defaults: Up/Down/Left/Right

        await f.ExecuteAsync(Hat(0)); // North → Up

        kb.Verify(k => k.KeyDown("Up"),    Times.Once);
        kb.Verify(k => k.KeyDown("Down"),  Times.Never);
        kb.Verify(k => k.KeyDown("Left"),  Times.Never);
        kb.Verify(k => k.KeyDown("Right"), Times.Never);
        kb.Verify(k => k.KeyUp(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Hat_DiagonalNE_PressesBothAdjacentKeys()
    {
        var d = MakeDescriptor(out var kb);
        var f = d.CreateFunctor(null);

        await f.ExecuteAsync(Hat(45));

        kb.Verify(k => k.KeyDown("Up"),    Times.Once);
        kb.Verify(k => k.KeyDown("Right"), Times.Once);
        kb.Verify(k => k.KeyDown("Down"),  Times.Never);
        kb.Verify(k => k.KeyDown("Left"),  Times.Never);
    }

    [Fact]
    public async Task Hat_Center_ReleasesAllCurrentlyHeldKeys()
    {
        var d = MakeDescriptor(out var kb);
        var f = d.CreateFunctor(null);

        await f.ExecuteAsync(Hat(45));   // Up + Right down
        kb.Invocations.Clear();

        await f.ExecuteAsync(Hat(-1));   // center

        kb.Verify(k => k.KeyUp("Up"),    Times.Once);
        kb.Verify(k => k.KeyUp("Right"), Times.Once);
        kb.Verify(k => k.KeyDown(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Hat_DirectionChange_EmitsOnlyDiff()
    {
        var d = MakeDescriptor(out var kb);
        var f = d.CreateFunctor(null);

        await f.ExecuteAsync(Hat(45));   // Up + Right held
        kb.Invocations.Clear();

        await f.ExecuteAsync(Hat(135));  // Down + Right (Right stays, Up→Down)

        // Right is shared between both directions: it must NOT be released and re-pressed.
        kb.Verify(k => k.KeyUp("Up"),     Times.Once);
        kb.Verify(k => k.KeyDown("Down"), Times.Once);
        kb.Verify(k => k.KeyUp("Right"),  Times.Never);
        kb.Verify(k => k.KeyDown("Right"), Times.Never);
    }

    [Fact]
    public async Task Hat_SameDirectionTwice_EmitsNoExtraEvents()
    {
        var d = MakeDescriptor(out var kb);
        var f = d.CreateFunctor(null);

        await f.ExecuteAsync(Hat(0));
        kb.Invocations.Clear();

        await f.ExecuteAsync(Hat(0));

        kb.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Hat_EmptyKey_SkippedSilently()
    {
        var d = MakeDescriptor(out var kb);
        var cfg = new JsonObject
        {
            ["upKey"]    = "",     // disabled
            ["downKey"]  = "Down",
            ["leftKey"]  = "Left",
            ["rightKey"] = "Right",
        };
        var f = d.CreateFunctor(cfg);

        await f.ExecuteAsync(Hat(0));      // Up — disabled, no key issued
        await f.ExecuteAsync(Hat(45));     // Up + Right — only Right issued

        kb.Verify(k => k.KeyDown("Up"),    Times.Never);
        kb.Verify(k => k.KeyDown(""),      Times.Never);
        kb.Verify(k => k.KeyDown("Right"), Times.Once);
    }

    [Fact]
    public async Task Hat_CustomKeys_UsesConfiguredKeyNames()
    {
        var d = MakeDescriptor(out var kb);
        var cfg = new JsonObject
        {
            ["upKey"]    = "W",
            ["downKey"]  = "S",
            ["leftKey"]  = "A",
            ["rightKey"] = "D",
        };
        var f = d.CreateFunctor(cfg);

        await f.ExecuteAsync(Hat(45));

        kb.Verify(k => k.KeyDown("W"), Times.Once);
        kb.Verify(k => k.KeyDown("D"), Times.Once);
    }

    // ── Buttons mode: shared state across 4 functors ─────────────────────────

    [Fact]
    public async Task Buttons_FourFunctorsShareState_PressUpThenRight_HoldsBoth()
    {
        var d = MakeDescriptor(out var kb);
        var cfg = MakeButtonsConfig(up: 10, down: 11, left: 12, right: 13);
        var upFunctor    = d.CreateFunctor(cfg);
        var rightFunctor = d.CreateFunctor(cfg);

        await upFunctor.ExecuteAsync(Button(10, pressed: true));
        await rightFunctor.ExecuteAsync(Button(13, pressed: true));

        kb.Verify(k => k.KeyDown("Up"),    Times.Once);
        kb.Verify(k => k.KeyDown("Right"), Times.Once);
        kb.Verify(k => k.KeyUp(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Buttons_ReleaseOne_OnlyThatKeyReleased()
    {
        var d = MakeDescriptor(out var kb);
        var cfg = MakeButtonsConfig(up: 10, down: 11, left: 12, right: 13);
        var upFunctor    = d.CreateFunctor(cfg);
        var rightFunctor = d.CreateFunctor(cfg);

        await upFunctor.ExecuteAsync(Button(10, pressed: true));
        await rightFunctor.ExecuteAsync(Button(13, pressed: true));
        kb.Invocations.Clear();

        await upFunctor.ExecuteAsync(Button(10, pressed: false));

        kb.Verify(k => k.KeyUp("Up"),    Times.Once);
        kb.Verify(k => k.KeyUp("Right"), Times.Never);
        kb.Verify(k => k.KeyDown(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Buttons_UnknownIdentifier_Ignored()
    {
        var d = MakeDescriptor(out var kb);
        var cfg = MakeButtonsConfig(up: 10, down: 11, left: 12, right: 13);
        var f = d.CreateFunctor(cfg);

        await f.ExecuteAsync(Button(99, pressed: true));

        kb.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Buttons_Threshold_TreatsHalfAsPressed()
    {
        var d = MakeDescriptor(out var kb);
        var cfg = MakeButtonsConfig(up: 10, down: 11, left: 12, right: 13);
        var f = d.CreateFunctor(cfg);

        await f.ExecuteAsync(new InputEvent(InputType.JoystickButton, Guid.Empty, 10, 0.49));
        kb.Verify(k => k.KeyDown("Up"), Times.Never);

        await f.ExecuteAsync(new InputEvent(InputType.JoystickButton, Guid.Empty, 10, 0.50));
        kb.Verify(k => k.KeyDown("Up"), Times.Once);
    }

    [Fact]
    public async Task Buttons_DifferentKeyTupleConfigs_DoNotShareState()
    {
        var d = MakeDescriptor(out var kb);
        var cfgA = MakeButtonsConfig(up: 10, down: 11, left: 12, right: 13);
        var cfgB = new JsonObject
        {
            ["upKey"] = "W", ["downKey"] = "S", ["leftKey"] = "A", ["rightKey"] = "D",
            ["upButtonId"] = 10, ["downButtonId"] = 11, ["leftButtonId"] = 12, ["rightButtonId"] = 13,
        };
        var fA = d.CreateFunctor(cfgA);
        var fB = d.CreateFunctor(cfgB);

        await fA.ExecuteAsync(Button(10, pressed: true));
        await fB.ExecuteAsync(Button(10, pressed: true));

        // Each tuple maintains its own state.
        kb.Verify(k => k.KeyDown("Up"), Times.Once);
        kb.Verify(k => k.KeyDown("W"),  Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MapToArrowKeysActionDescriptor MakeDescriptor(out Mock<IKeyboardSimulator> keyboard)
    {
        keyboard = new Mock<IKeyboardSimulator>();
        return new MapToArrowKeysActionDescriptor(
            keyboard.Object,
            NullLogger<MapToArrowKeysActionDescriptor>.Instance);
    }

    private static JsonObject MakeButtonsConfig(int up, int down, int left, int right) => new()
    {
        ["upKey"]        = "Up",
        ["downKey"]      = "Down",
        ["leftKey"]      = "Left",
        ["rightKey"]     = "Right",
        ["upButtonId"]   = up,
        ["downButtonId"] = down,
        ["leftButtonId"] = left,
        ["rightButtonId"]= right,
    };
}
