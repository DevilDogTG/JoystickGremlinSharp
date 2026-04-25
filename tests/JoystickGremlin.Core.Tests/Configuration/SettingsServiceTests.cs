// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;
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
        svc.Settings.VJoyDeviceId.Should().Be(1u);
        svc.Settings.LastProfilePath.Should().BeNull();
    }

    // ── Save → Load round-trip ─────────────────────────────────────────────

    [Fact]
    public async Task SaveThenLoad_Roundtrips_AllProperties()
    {
        var svc = CreateService();
        svc.Settings.LastProfilePath = @"C:\Profiles\test.json";
        svc.Settings.VJoyDeviceId = 3;
        svc.Settings.DefaultModeName = "Combat";
        svc.Settings.StartMinimized = true;

        await svc.SaveAsync();

        var svc2 = CreateService();
        await svc2.LoadAsync();

        svc2.Settings.LastProfilePath.Should().Be(@"C:\Profiles\test.json");
        svc2.Settings.VJoyDeviceId.Should().Be(3u);
        svc2.Settings.DefaultModeName.Should().Be("Combat");
        svc2.Settings.StartMinimized.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_MissingFile_FallsBackToDefaults()
    {
        var svc = CreateService("nonexistent_subdir");

        await svc.LoadAsync();

        svc.Settings.VJoyDeviceId.Should().Be(1u);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private SettingsService CreateService(string? subfolder = null)
    {
        var dir = subfolder is null ? _tempDir : Path.Combine(_tempDir, subfolder);
        var path = Path.Combine(dir, "settings.json");
        return new SettingsService(NullLogger<SettingsService>.Instance, path);
    }
}
