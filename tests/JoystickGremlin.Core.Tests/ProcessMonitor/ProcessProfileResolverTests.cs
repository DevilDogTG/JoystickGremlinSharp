// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.ProcessMonitor;
using JoystickGremlin.Core.Profile;
using Xunit;

namespace JoystickGremlin.Core.Tests.ProcessMonitor;

public sealed class ProcessProfileResolverTests
{
    private static ProcessTrigger NameTrigger(string exeName, bool enabled = true) => new()
    {
        MatchType      = ProcessMatchType.ExecutableName,
        ExecutableName = exeName,
        IsEnabled      = enabled,
    };

    private static ProcessTrigger PathTrigger(string exePath, bool enabled = true) => new()
    {
        MatchType      = ProcessMatchType.ExecutablePath,
        ExecutablePath = exePath,
        IsEnabled      = enabled,
    };

    private static ProfileEntry Entry(string name, params ProcessTrigger[] triggers) =>
        new(name, null, $@"C:\fake\{name}.json", triggers);

    // ──────────────────────────────────────────────
    // Name match
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_ExecutableName_ReturnsMatchWithOwningProfile()
    {
        var profile = Entry("DCS", NameTrigger("DCS.exe"));

        var result = ProcessProfileResolver.Resolve(@"C:\Program Files\DCS World\bin\DCS.exe", [profile]);

        result.Should().NotBeNull();
        result!.Profile.Should().BeSameAs(profile);
        result.Trigger.ExecutableName.Should().Be("DCS.exe");
    }

    [Fact]
    public void Resolve_ExecutableName_IsCaseInsensitive()
    {
        var profile = Entry("DCS", NameTrigger("dcs.EXE"));

        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS World\DCS.exe", [profile]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_IgnoresDirectory()
    {
        var profile = Entry("Game", NameTrigger("game.exe"));

        var result = ProcessProfileResolver.Resolve(@"D:\Some\Other\Path\game.exe", [profile]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_DifferentExe_ReturnsNull()
    {
        var profile = Entry("DCS", NameTrigger("DCS.exe"));

        var result = ProcessProfileResolver.Resolve(@"C:\Games\OtherGame.exe", [profile]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_Empty_ReturnsNull()
    {
        var profile = Entry("Blank", NameTrigger(string.Empty));

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", [profile]);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // Path match
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_ExactPath_ReturnsMatch()
    {
        var profile = Entry("Exact", PathTrigger(@"C:\Games\game.exe"));

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", [profile]);

        result.Should().NotBeNull();
        result!.Profile.Should().BeSameAs(profile);
    }

    [Fact]
    public void Resolve_ExactPath_IsCaseInsensitive()
    {
        var profile = Entry("Exact", PathTrigger(@"C:\games\Game.EXE"));

        var result = ProcessProfileResolver.Resolve(@"c:\Games\game.exe", [profile]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExactPath_NormalizesBackslashes()
    {
        var profile = Entry("Exact", PathTrigger("C:/Games/game.exe"));

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", [profile]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_PathMode_DifferentDirectory_ReturnsNull()
    {
        var profile = Entry("Exact", PathTrigger(@"C:\Games\game.exe"));

        var result = ProcessProfileResolver.Resolve(@"D:\Other\game.exe", [profile]);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // Priority and ordering — within a profile and across profiles
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_FirstMatchWithinProfile_ReturnsFirstTrigger()
    {
        var first  = NameTrigger("DCS.exe");
        var second = NameTrigger("DCS.exe");
        var profile = Entry("DCS", first, second);

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [profile]);

        result.Should().NotBeNull();
        result!.Trigger.Should().BeSameAs(first);
    }

    [Fact]
    public void Resolve_FirstMatchAcrossProfiles_ReturnsFirstProfile()
    {
        var alpha = Entry("Alpha", NameTrigger("DCS.exe"));
        var beta  = Entry("Beta",  NameTrigger("DCS.exe"));

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [alpha, beta]);

        result.Should().NotBeNull();
        result!.Profile.Should().BeSameAs(alpha);
    }

    [Fact]
    public void Resolve_MixedModes_FirstEnabledMatchWins()
    {
        var profile = Entry("Mixed",
            PathTrigger(@"C:\Games\DCS\DCS.exe"),
            NameTrigger("DCS.exe"));

        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS\DCS.exe", [profile]);

        result.Should().NotBeNull();
        result!.Trigger.MatchType.Should().Be(ProcessMatchType.ExecutablePath);
    }

    // ──────────────────────────────────────────────
    // Disabled entries
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_DisabledTrigger_IsSkipped()
    {
        var profile = Entry("DCS", NameTrigger("DCS.exe", enabled: false));

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [profile]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_DisabledTriggerWithEnabledFallback_ReturnsFallback()
    {
        var disabled = NameTrigger("DCS.exe", enabled: false);
        var enabled  = NameTrigger("DCS.exe", enabled: true);
        var profile = Entry("DCS", disabled, enabled);

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [profile]);

        result.Should().NotBeNull();
        result!.Trigger.Should().BeSameAs(enabled);
    }

    // ──────────────────────────────────────────────
    // Edge cases
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_NullPath_ReturnsNull()
    {
        var profile = Entry("X", NameTrigger("game.exe"));

        var result = ProcessProfileResolver.Resolve(null!, [profile]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        var profile = Entry("X", NameTrigger("game.exe"));

        var result = ProcessProfileResolver.Resolve(string.Empty, [profile]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyProfileList_ReturnsNull()
    {
        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", []);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_ProfileWithNoTriggers_ReturnsNull()
    {
        var profile = Entry("Empty");

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", [profile]);

        result.Should().BeNull();
    }
}
