// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.ProcessMonitor;
using Xunit;

namespace JoystickGremlin.Core.Tests.ProcessMonitor;

public sealed class ProcessProfileResolverTests
{
    private static AutoLoadTrigger NameTrigger(
        string exeName, string profile = "Default", bool enabled = true) => new()
    {
        ProfilePath    = $@"C:\fake\{profile}.json",
        MatchType      = ProcessMatchType.ExecutableName,
        ExecutableName = exeName,
        IsEnabled      = enabled,
    };

    private static AutoLoadTrigger PathTrigger(
        string exePath, string profile = "Default", bool enabled = true) => new()
    {
        ProfilePath    = $@"C:\fake\{profile}.json",
        MatchType      = ProcessMatchType.ExecutablePath,
        ExecutablePath = exePath,
        IsEnabled      = enabled,
    };

    // ──────────────────────────────────────────────
    // Name match
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_ExecutableName_ReturnsTriggerWithProfilePath()
    {
        var trigger = NameTrigger("DCS.exe", profile: "DCS");

        var result = ProcessProfileResolver.Resolve(@"C:\Program Files\DCS World\bin\DCS.exe", [trigger]);

        result.Should().BeSameAs(trigger);
        result!.ProfilePath.Should().Be(@"C:\fake\DCS.json");
    }

    [Fact]
    public void Resolve_ExecutableName_IsCaseInsensitive()
    {
        var trigger = NameTrigger("dcs.EXE");

        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS World\DCS.exe", [trigger]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_IgnoresDirectory()
    {
        var trigger = NameTrigger("game.exe");

        var result = ProcessProfileResolver.Resolve(@"D:\Some\Other\Path\game.exe", [trigger]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_DifferentExe_ReturnsNull()
    {
        var trigger = NameTrigger("DCS.exe");

        var result = ProcessProfileResolver.Resolve(@"C:\Games\OtherGame.exe", [trigger]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_Empty_ReturnsNull()
    {
        var trigger = NameTrigger(string.Empty);

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", [trigger]);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // Path match
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_ExactPath_ReturnsMatch()
    {
        var trigger = PathTrigger(@"C:\Games\game.exe");

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", [trigger]);

        result.Should().BeSameAs(trigger);
    }

    [Fact]
    public void Resolve_ExactPath_IsCaseInsensitive()
    {
        var trigger = PathTrigger(@"C:\games\Game.EXE");

        var result = ProcessProfileResolver.Resolve(@"c:\Games\game.exe", [trigger]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExactPath_NormalizesBackslashes()
    {
        var trigger = PathTrigger("C:/Games/game.exe");

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", [trigger]);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_PathMode_DifferentDirectory_ReturnsNull()
    {
        var trigger = PathTrigger(@"C:\Games\game.exe");

        var result = ProcessProfileResolver.Resolve(@"D:\Other\game.exe", [trigger]);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // Priority and ordering — list order, first enabled match wins
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_TwoMatchingTriggers_ReturnsFirstInList()
    {
        var first  = NameTrigger("DCS.exe");
        var second = NameTrigger("DCS.exe");

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [first, second]);

        result.Should().BeSameAs(first);
    }

    [Fact]
    public void Resolve_TwoMatchingTriggersForDifferentProfiles_ReturnsFirstInList()
    {
        var alpha = NameTrigger("DCS.exe", profile: "Alpha");
        var beta  = NameTrigger("DCS.exe", profile: "Beta");

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [alpha, beta]);

        result.Should().BeSameAs(alpha);
    }

    [Fact]
    public void Resolve_MixedModes_FirstEnabledMatchWins()
    {
        var path = PathTrigger(@"C:\Games\DCS\DCS.exe");
        var name = NameTrigger("DCS.exe");

        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS\DCS.exe", [path, name]);

        result.Should().BeSameAs(path);
    }

    // ──────────────────────────────────────────────
    // Disabled entries
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_DisabledTrigger_IsSkipped()
    {
        var trigger = NameTrigger("DCS.exe", enabled: false);

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [trigger]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_DisabledTriggerWithEnabledFallback_ReturnsFallback()
    {
        var disabled = NameTrigger("DCS.exe", enabled: false);
        var enabled  = NameTrigger("DCS.exe", enabled: true);

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", [disabled, enabled]);

        result.Should().BeSameAs(enabled);
    }

    // ──────────────────────────────────────────────
    // Edge cases
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_NullPath_ReturnsNull()
    {
        var trigger = NameTrigger("game.exe");

        var result = ProcessProfileResolver.Resolve(null!, [trigger]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        var trigger = NameTrigger("game.exe");

        var result = ProcessProfileResolver.Resolve(string.Empty, [trigger]);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyTriggerList_ReturnsNull()
    {
        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", []);

        result.Should().BeNull();
    }
}
