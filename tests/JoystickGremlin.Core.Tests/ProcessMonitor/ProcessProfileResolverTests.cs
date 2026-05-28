// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ProcessMonitor;
using Xunit;

namespace JoystickGremlin.Core.Tests.ProcessMonitor;

public sealed class ProcessProfileResolverTests
{
    private static ProcessProfileMapping NameMapping(
        string exeName, string profile = "profile.json", bool enabled = true) =>
        new()
        {
            MatchType      = ProcessMatchType.ExecutableName,
            ExecutableName = exeName,
            ProfilePath    = profile,
            IsEnabled      = enabled,
        };

    private static ProcessProfileMapping PathMapping(
        string exePath, string profile = "profile.json", bool enabled = true) =>
        new()
        {
            MatchType      = ProcessMatchType.ExecutablePath,
            ExecutablePath = exePath,
            ProfilePath    = profile,
            IsEnabled      = enabled,
        };

    // ──────────────────────────────────────────────
    // Name match
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_ExecutableName_ReturnsMatch()
    {
        var mappings = new List<ProcessProfileMapping> { NameMapping("DCS.exe", "dcs.json") };

        var result = ProcessProfileResolver.Resolve(@"C:\Program Files\DCS World\bin\DCS.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("dcs.json");
    }

    [Fact]
    public void Resolve_ExecutableName_IsCaseInsensitive()
    {
        var mappings = new List<ProcessProfileMapping> { NameMapping("dcs.EXE") };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS World\DCS.exe", mappings);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_IgnoresDirectory()
    {
        // Same exe name in a completely different install location still matches.
        var mappings = new List<ProcessProfileMapping> { NameMapping("game.exe") };

        var result = ProcessProfileResolver.Resolve(@"D:\Some\Other\Path\game.exe", mappings);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_DifferentExe_ReturnsNull()
    {
        var mappings = new List<ProcessProfileMapping> { NameMapping("DCS.exe") };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\OtherGame.exe", mappings);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_ExecutableName_Empty_ReturnsNull()
    {
        var mappings = new List<ProcessProfileMapping> { NameMapping(string.Empty) };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", mappings);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // Path match (legacy / manual mode)
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_ExactPath_ReturnsMatch()
    {
        var mappings = new List<ProcessProfileMapping> { PathMapping(@"C:\Games\game.exe", "exact.json") };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("exact.json");
    }

    [Fact]
    public void Resolve_ExactPath_IsCaseInsensitive()
    {
        var mappings = new List<ProcessProfileMapping> { PathMapping(@"C:\games\Game.EXE") };

        var result = ProcessProfileResolver.Resolve(@"c:\Games\game.exe", mappings);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExactPath_NormalizesBackslashes()
    {
        var mappings = new List<ProcessProfileMapping> { PathMapping("C:/Games/game.exe") };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", mappings);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_PathMode_DifferentDirectory_ReturnsNull()
    {
        // Path mode must NOT match on file name alone — the full path has to agree.
        var mappings = new List<ProcessProfileMapping> { PathMapping(@"C:\Games\game.exe") };

        var result = ProcessProfileResolver.Resolve(@"D:\Other\game.exe", mappings);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // Priority and ordering
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_FirstMatchWins_ReturnsFirstInList()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            NameMapping("DCS.exe", "first.json"),
            NameMapping("DCS.exe", "second.json"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("first.json");
    }

    [Fact]
    public void Resolve_MixedModes_FirstEnabledMatchWins()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            PathMapping(@"C:\Games\DCS\DCS.exe", "path.json"),
            NameMapping("DCS.exe", "name.json"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS\DCS.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("path.json");
    }

    // ──────────────────────────────────────────────
    // Disabled entries
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_DisabledEntry_IsSkipped()
    {
        var mappings = new List<ProcessProfileMapping> { NameMapping("DCS.exe", enabled: false) };

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", mappings);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_DisabledEntryWithEnabledFallback_ReturnsFallback()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            NameMapping("DCS.exe", "disabled.json", enabled: false),
            NameMapping("DCS.exe", "enabled.json",  enabled: true),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("enabled.json");
    }

    // ──────────────────────────────────────────────
    // Edge cases
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_NullPath_ReturnsNull()
    {
        var mappings = new List<ProcessProfileMapping> { NameMapping("game.exe") };

        var result = ProcessProfileResolver.Resolve(null!, mappings);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        var mappings = new List<ProcessProfileMapping> { NameMapping("game.exe") };

        var result = ProcessProfileResolver.Resolve(string.Empty, mappings);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyMappings_ReturnsNull()
    {
        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", []);

        result.Should().BeNull();
    }
}
