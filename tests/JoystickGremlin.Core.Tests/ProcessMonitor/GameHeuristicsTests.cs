// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.ProcessMonitor;
using Xunit;

namespace JoystickGremlin.Core.Tests.ProcessMonitor;

public sealed class GameHeuristicsTests
{
    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\DCSWorld\bin\DCS.exe")]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Elite\Elite.exe")]
    [InlineData(@"C:\Program Files\Epic Games\Fortnite\Fortnite.exe")]
    [InlineData(@"C:\GOG Games\Witcher 3\witcher3.exe")]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.FlightSim\FlightSim.exe")]
    [InlineData(@"C:/Riot Games/VALORANT/live/VALORANT.exe")]
    public void IsLikelyGame_KnownStorePaths_ReturnsTrue(string path)
    {
        GameHeuristics.IsLikelyGame(path).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\notepad.exe")]
    [InlineData(@"C:\Program Files\Mozilla Firefox\firefox.exe")]
    [InlineData(@"C:\Users\me\AppData\Local\Discord\Discord.exe")]
    public void IsLikelyGame_NonGamePaths_ReturnsFalse(string path)
    {
        GameHeuristics.IsLikelyGame(path).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsLikelyGame_EmptyOrNull_ReturnsFalse(string? path)
    {
        GameHeuristics.IsLikelyGame(path).Should().BeFalse();
    }
}
