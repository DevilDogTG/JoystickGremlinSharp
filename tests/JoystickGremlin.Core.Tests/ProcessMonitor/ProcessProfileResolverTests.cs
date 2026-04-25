// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ProcessMonitor;
using Xunit;

namespace JoystickGremlin.Core.Tests.ProcessMonitor;

public sealed class ProcessProfileResolverTests
{
    private static ProcessProfileMapping MakeMapping(
        string exe, string profile = "profile.json", bool enabled = true) =>
        new()
        {
            ExecutablePath = exe,
            ProfilePath    = profile,
            IsEnabled      = enabled,
        };

    // ──────────────────────────────────────────────
    // Exact match
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_ExactPath_ReturnsMatch()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(@"C:\Games\game.exe"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("profile.json");
    }

    [Fact]
    public void Resolve_ExactPath_IsCaseInsensitive()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(@"C:\games\Game.EXE"),
        };

        var result = ProcessProfileResolver.Resolve(@"c:\Games\game.exe", mappings);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ExactPath_NormalizesBackslashes()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping("C:/Games/game.exe"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", mappings);

        result.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────
    // Regex fallback
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_RegexPattern_ReturnsMatch()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(@".*DCS.*", "dcs-profile.json"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\Program Files\DCS World\bin\DCS.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("dcs-profile.json");
    }

    [Fact]
    public void Resolve_RegexPattern_IsCaseInsensitive()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(".*dcs.*"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS World\DCS.EXE", mappings);

        result.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────
    // Priority and ordering
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_FirstMatchWins_ReturnsFirstInList()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(@".*DCS.*",  "first.json"),
            MakeMapping(@".*DCS.*",  "second.json"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("first.json");
    }

    [Fact]
    public void Resolve_ExactMatchBeforeRegex()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(@".*DCS.*",               "regex.json"),
            MakeMapping(@"C:\Games\DCS\DCS.exe",  "exact.json"),
        };

        // Exact match is in position 1, regex in position 0 — but exact wins regardless.
        var result = ProcessProfileResolver.Resolve(@"C:\Games\DCS\DCS.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("exact.json");
    }

    // ──────────────────────────────────────────────
    // Disabled entries
    // ──────────────────────────────────────────────

    [Fact]
    public void Resolve_DisabledEntry_IsSkipped()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(@".*DCS.*", enabled: false),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\DCS\DCS.exe", mappings);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_DisabledEntryWithEnabledFallback_ReturnsFallback()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping(@".*DCS.*", "disabled.json", enabled: false),
            MakeMapping(@".*DCS.*", "enabled.json",  enabled: true),
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
        var mappings = new List<ProcessProfileMapping> { MakeMapping("game.exe") };

        var result = ProcessProfileResolver.Resolve(null!, mappings);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        var mappings = new List<ProcessProfileMapping> { MakeMapping("game.exe") };

        var result = ProcessProfileResolver.Resolve(string.Empty, mappings);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyMappings_ReturnsNull()
    {
        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", []);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_InvalidRegex_IsSkipped()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping("[[[invalid regex"),
        };

        // Should not throw; invalid regex entry is silently skipped.
        var act = () => ProcessProfileResolver.Resolve(@"C:\Games\game.exe", mappings);

        act.Should().NotThrow();
    }

    [Fact]
    public void Resolve_InvalidRegexFollowedByValidMatch_ReturnsValidMatch()
    {
        var mappings = new List<ProcessProfileMapping>
        {
            MakeMapping("[[[invalid",   "bad.json"),
            MakeMapping(@".*game\.exe", "good.json"),
        };

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", mappings);

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("good.json");
    }
}
