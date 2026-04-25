// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Exceptions;
using JoystickGremlin.Core.Modes;
using Microsoft.Extensions.Logging.Abstractions;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;
using Mode = JoystickGremlin.Core.Profile.Mode;

namespace JoystickGremlin.Core.Tests.Modes;

public sealed class ModeManagerTests
{
    private readonly IModeManager _sut = new ModeManager(NullLogger<ModeManager>.Instance);

    private static ProfileModel MakeProfile(params (string name, string? parent)[] modes)
    {
        var profile = new ProfileModel { Name = "Test" };
        foreach (var (name, parent) in modes)
            profile.Modes.Add(new Mode { Name = name, ParentModeName = parent });
        return profile;
    }

    // ── Reset ──────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_WithProfile_SetsActiveToFirstMode()
    {
        var profile = MakeProfile(("Root", null), ("Child", "Root"));

        _sut.Reset(profile);

        _sut.ActiveModeName.Should().Be("Root");
    }

    [Fact]
    public void Reset_RaisesModChangedEvent()
    {
        var profile = MakeProfile(("Default", null));
        string? received = null;
        _sut.ModeChanged += (_, name) => received = name;

        _sut.Reset(profile);

        received.Should().Be("Default");
    }

    // ── SwitchTo ───────────────────────────────────────────────────────────

    [Fact]
    public void SwitchTo_ValidMode_ChangesActiveMode()
    {
        var profile = MakeProfile(("Root", null), ("Combat", null));
        _sut.Reset(profile);

        _sut.SwitchTo("Combat");

        _sut.ActiveModeName.Should().Be("Combat");
    }

    [Fact]
    public void SwitchTo_ClearsTemporaryModes()
    {
        var profile = MakeProfile(("Base", null), ("Temp", null), ("Other", null));
        _sut.Reset(profile);
        _sut.PushTemporary("Temp");

        _sut.SwitchTo("Other");

        _sut.ActiveModeName.Should().Be("Other");
        // Popping should return false (no temporaries remain)
        _sut.PopTemporary().Should().BeFalse();
    }

    [Fact]
    public void SwitchTo_UnknownMode_ThrowsModeException()
    {
        var profile = MakeProfile(("Root", null));
        _sut.Reset(profile);

        var act = () => _sut.SwitchTo("NonExistent");

        act.Should().Throw<ModeException>();
    }

    // ── PushTemporary / PopTemporary ───────────────────────────────────────

    [Fact]
    public void PushTemporary_ThenPop_RestoresPreviousMode()
    {
        var profile = MakeProfile(("Base", null), ("Overlay", null));
        _sut.Reset(profile);

        _sut.PushTemporary("Overlay");
        _sut.ActiveModeName.Should().Be("Overlay");

        var popped = _sut.PopTemporary();
        popped.Should().BeTrue();
        _sut.ActiveModeName.Should().Be("Base");
    }

    [Fact]
    public void PopTemporary_WithNoTemporaries_ReturnsFalse()
    {
        var profile = MakeProfile(("Base", null));
        _sut.Reset(profile);

        _sut.PopTemporary().Should().BeFalse();
    }

    [Fact]
    public void PushTemporary_MultipleLevels_PopsCorrectly()
    {
        var profile = MakeProfile(("A", null), ("B", null), ("C", null));
        _sut.Reset(profile);
        _sut.PushTemporary("B");
        _sut.PushTemporary("C");

        _sut.PopTemporary();
        _sut.ActiveModeName.Should().Be("B");
        _sut.PopTemporary();
        _sut.ActiveModeName.Should().Be("A");
    }

    // ── GetInheritanceChain ────────────────────────────────────────────────

    [Fact]
    public void GetInheritanceChain_NoParent_ReturnsJustSelf()
    {
        var profile = MakeProfile(("Root", null));

        var chain = _sut.GetInheritanceChain("Root", profile);

        chain.Should().ContainSingle().Which.Should().Be("Root");
    }

    [Fact]
    public void GetInheritanceChain_LinearChain_ReturnsFromChildToRoot()
    {
        var profile = MakeProfile(("Root", null), ("Mid", "Root"), ("Leaf", "Mid"));

        var chain = _sut.GetInheritanceChain("Leaf", profile);

        chain.Should().Equal("Leaf", "Mid", "Root");
    }

    [Fact]
    public void GetInheritanceChain_CircularReference_BreaksWithoutInfiniteLoop()
    {
        // Manually create a circular reference.
        var profile = new ProfileModel { Name = "Circular" };
        profile.Modes.Add(new Mode { Name = "A", ParentModeName = "B" });
        profile.Modes.Add(new Mode { Name = "B", ParentModeName = "A" });

        var act = () => _sut.GetInheritanceChain("A", profile);

        // Should not throw; circular detection breaks the loop.
        act.Should().NotThrow();
    }
}
