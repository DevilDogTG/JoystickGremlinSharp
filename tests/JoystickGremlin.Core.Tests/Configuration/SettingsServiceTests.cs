// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ProcessMonitor;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Configuration;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jg_settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── Default settings ───────────────────────────────────────────────────

    [Fact]
    public void Settings_BeforeLoad_ReturnsDefaultInstance()
    {
        var svc = CreateService();

        svc.Settings.Should().NotBeNull();
        svc.Settings.ActiveProfilePath.Should().BeNull();
        svc.Settings.UiUpdateIntervalMs.Should().Be(10);
    }

    // ── Save → Load round-trip ─────────────────────────────────────────────

    [Fact]
    public async Task SaveThenLoad_Roundtrips_AllProperties()
    {
        var svc = CreateService();
        svc.Settings.ActiveProfilePath = @"C:\Profiles\test.json";
        svc.Settings.UiUpdateIntervalMs = 5;
        svc.Settings.ProfilesFolderPath = @"C:\Profiles";
        svc.Settings.StartMinimized = true;

        await svc.SaveAsync();

        var svc2 = CreateService();
        await svc2.LoadAsync();

        svc2.Settings.ActiveProfilePath.Should().Be(@"C:\Profiles\test.json");
        svc2.Settings.UiUpdateIntervalMs.Should().Be(5);
        svc2.Settings.ProfilesFolderPath.Should().Be(@"C:\Profiles");
        svc2.Settings.StartMinimized.Should().BeTrue();
    }

    [Fact]
    public async Task SaveThenLoad_AutoLoadTriggers_RoundTripCorrectly()
    {
        var svc = CreateService();
        svc.Settings.AutoLoadTriggers =
        [
            new AutoLoadTrigger
            {
                ProfilePath             = @"C:\Profiles\DCS.json",
                MatchType               = ProcessMatchType.ExecutableName,
                ExecutableName          = "DCS.exe",
                ExecutablePath          = "C:/Games/DCS/DCS.exe",
                IsEnabled               = true,
                AutoStart               = true,
                RemainActiveOnFocusLoss = false,
            },
            new AutoLoadTrigger
            {
                ProfilePath             = @"C:\Profiles\Other.json",
                MatchType               = ProcessMatchType.ExecutablePath,
                ExecutablePath          = "C:/Games/Other/other.exe",
                IsEnabled               = false,
                AutoStart               = false,
                RemainActiveOnFocusLoss = true,
            },
        ];

        await svc.SaveAsync();

        var svc2 = CreateService();
        await svc2.LoadAsync();

        var triggers = svc2.Settings.AutoLoadTriggers;
        triggers.Should().HaveCount(2);
        triggers[0].ProfilePath.Should().Be(@"C:\Profiles\DCS.json");
        triggers[0].MatchType.Should().Be(ProcessMatchType.ExecutableName);
        triggers[0].ExecutableName.Should().Be("DCS.exe");
        triggers[0].IsEnabled.Should().BeTrue();
        triggers[1].MatchType.Should().Be(ProcessMatchType.ExecutablePath);
        triggers[1].IsEnabled.Should().BeFalse();
        triggers[1].RemainActiveOnFocusLoss.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_AutoLoadTriggerMatchType_IsSerializedAsString()
    {
        var svc = CreateService();
        svc.Settings.AutoLoadTriggers =
        [
            new AutoLoadTrigger { MatchType = ProcessMatchType.ExecutableName },
        ];

        await svc.SaveAsync();

        var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "settings.json"));
        json.Should().Contain("\"ExecutableName\"");
    }

    [Fact]
    public async Task LoadAsync_MissingFile_FallsBackToDefaults()
    {
        var svc = CreateService("nonexistent_subdir");

        await svc.LoadAsync();

        svc.Settings.UiUpdateIntervalMs.Should().Be(10);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private SettingsService CreateService(string? subfolder = null)
    {
        var dir = subfolder is null ? _tempDir : Path.Combine(_tempDir, subfolder);
        var path = Path.Combine(dir, "settings.json");
        return new SettingsService(NullLogger<SettingsService>.Instance, path);
    }
}
