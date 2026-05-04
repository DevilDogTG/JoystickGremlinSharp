// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions.Macro;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Profile;

namespace JoystickGremlin.Core.Tests.Profile;

/// <summary>
/// Verifies that BoundAction.Configuration JSON round-trips correctly for every built-in action type.
/// </summary>
public sealed class BoundActionSerializationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IProfileRepository _sut;

    public BoundActionSerializationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jg_ser_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sut = new ProfileRepository();
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static JoystickGremlin.Core.Profile.Profile ProfileWithAction(BoundAction action)
    {
        var binding = new InputBinding
        {
            DeviceGuid = Guid.Empty,
            InputType  = InputType.JoystickButton,
            Identifier = 1,
            Actions    = [action],
        };
        return new JoystickGremlin.Core.Profile.Profile { Name = "SerTest", Bindings = [binding] };
    }

    private async Task<BoundAction> RoundTrip(BoundAction action)
    {
        var path    = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.json");
        var profile = ProfileWithAction(action);

        await _sut.SaveAsync(profile, path);
        var loaded = await _sut.LoadAsync(path);

        return loaded.Bindings[0].Actions[0];
    }

    // ── vJoy Axis ───────────────────────────────────────────────────────────

    [Fact]
    public async Task VJoyAxis_Config_RoundTrips()
    {
        var action = new BoundAction
        {
            ActionTag     = VJoyAxisDescriptor.ActionTag,
            Configuration = new JsonObject { ["vjoyId"] = 2, ["axisIndex"] = 5 },
        };

        var loaded = await RoundTrip(action);

        loaded.ActionTag.Should().Be(VJoyAxisDescriptor.ActionTag);
        loaded.Configuration!["vjoyId"]!.GetValue<int>().Should().Be(2);
        loaded.Configuration["axisIndex"]!.GetValue<int>().Should().Be(5);
    }

    // ── vJoy Button ─────────────────────────────────────────────────────────

    [Fact]
    public async Task VJoyButton_Config_RoundTrips()
    {
        var action = new BoundAction
        {
            ActionTag     = VJoyButtonDescriptor.ActionTag,
            Configuration = new JsonObject { ["vjoyId"] = 1, ["buttonIndex"] = 10 },
        };

        var loaded = await RoundTrip(action);

        loaded.ActionTag.Should().Be(VJoyButtonDescriptor.ActionTag);
        loaded.Configuration!["vjoyId"]!.GetValue<int>().Should().Be(1);
        loaded.Configuration["buttonIndex"]!.GetValue<int>().Should().Be(10);
    }

    // ── vJoy Hat ────────────────────────────────────────────────────────────

    [Fact]
    public async Task VJoyHat_Config_RoundTrips()
    {
        var action = new BoundAction
        {
            ActionTag     = VJoyHatDescriptor.ActionTag,
            Configuration = new JsonObject { ["vjoyId"] = 3, ["hatIndex"] = 2 },
        };

        var loaded = await RoundTrip(action);

        loaded.ActionTag.Should().Be(VJoyHatDescriptor.ActionTag);
        loaded.Configuration!["vjoyId"]!.GetValue<int>().Should().Be(3);
        loaded.Configuration["hatIndex"]!.GetValue<int>().Should().Be(2);
    }

    // ── Macro ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Macro_Config_RoundTrips()
    {
        var action = new BoundAction
        {
            ActionTag     = MacroActionDescriptor.ActionTag,
            Configuration = new JsonObject { ["keys"] = "LControl,C", ["onPress"] = false },
        };

        var loaded = await RoundTrip(action);

        loaded.ActionTag.Should().Be(MacroActionDescriptor.ActionTag);
        loaded.Configuration!["keys"]!.GetValue<string>().Should().Be("LControl,C");
        loaded.Configuration["onPress"]!.GetValue<bool>().Should().BeFalse();
    }

    // ── Null configuration ───────────────────────────────────────────────────

    [Fact]
    public async Task BoundAction_NullConfiguration_RoundTrips()
    {
        var action = new BoundAction
        {
            ActionTag     = VJoyAxisDescriptor.ActionTag,
            Configuration = null,
        };

        var loaded = await RoundTrip(action);

        loaded.ActionTag.Should().Be(VJoyAxisDescriptor.ActionTag);
        loaded.Configuration.Should().BeNull();
    }
}

