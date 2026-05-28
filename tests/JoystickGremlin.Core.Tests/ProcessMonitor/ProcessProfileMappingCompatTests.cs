// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ProcessMonitor;
using Xunit;

namespace JoystickGremlin.Core.Tests.ProcessMonitor;

/// <summary>
/// Verifies that process mappings persisted before the <see cref="ProcessMatchType"/> field
/// existed (a bare <c>ExecutablePath</c>) still deserialize and resolve correctly.
/// </summary>
public sealed class ProcessProfileMappingCompatTests
{
    [Fact]
    public void LegacyMapping_WithoutMatchType_DeserializesToPathMode()
    {
        const string json = """
        {
            "ExecutablePath": "C:/Games/game.exe",
            "ProfilePath": "p.json",
            "IsEnabled": true,
            "AutoStart": true,
            "RemainActiveOnFocusLoss": false
        }
        """;

        var mapping = JsonSerializer.Deserialize<ProcessProfileMapping>(json);

        mapping.Should().NotBeNull();
        mapping!.MatchType.Should().Be(ProcessMatchType.ExecutablePath);
        mapping.ExecutableName.Should().BeEmpty();
        mapping.ExecutablePath.Should().Be("C:/Games/game.exe");
    }

    [Fact]
    public void LegacyMapping_WithoutMatchType_ResolvesByPath()
    {
        const string json = """{ "ExecutablePath": "C:/Games/game.exe", "ProfilePath": "p.json", "IsEnabled": true }""";
        var mapping = JsonSerializer.Deserialize<ProcessProfileMapping>(json)!;

        var result = ProcessProfileResolver.Resolve(@"C:\Games\game.exe", new List<ProcessProfileMapping> { mapping });

        result.Should().NotBeNull();
        result!.ProfilePath.Should().Be("p.json");
    }
}
