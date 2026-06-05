// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.ProcessMonitor;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.ProcessMonitor;

public sealed class AutoLoadTriggerMigratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppSettings _settings = new();
    private readonly Mock<ISettingsService> _settingsMock;
    private readonly AutoLoadTriggerMigrator _sut;

    public AutoLoadTriggerMigratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jg_migrator_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _settingsMock = new Mock<ISettingsService>();
        _settingsMock.SetupGet(s => s.Settings).Returns(_settings);
        _settingsMock.Setup(s => s.SaveAsync(It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);

        var libraryMock = new Mock<IProfileLibrary>();
        libraryMock.SetupGet(l => l.ProfilesFolder).Returns(_tempDir);

        _sut = new AutoLoadTriggerMigrator(
            _settingsMock.Object,
            libraryMock.Object,
            NullLogger<AutoLoadTriggerMigrator>.Instance);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<string> WriteProfileWithTriggersAsync(
        string fileName, string? category = null, params string[] exeNames)
    {
        var dir = category is null ? _tempDir : Directory.CreateDirectory(Path.Combine(_tempDir, category)).FullName;
        var triggers = string.Join(",", exeNames.Select(exe => $$"""
            {
              "matchType": "ExecutableName",
              "executableName": "{{exe}}",
              "executablePath": "C:/Games/{{exe}}",
              "isEnabled": true,
              "autoStart": true,
              "remainActiveOnFocusLoss": false
            }
            """));
        var path = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(path, $$"""
            {
              "name": "{{Path.GetFileNameWithoutExtension(fileName)}}",
              "bindings": [],
              "autoLoadTriggers": [{{triggers}}]
            }
            """);
        return path;
    }

    private async Task<string> WriteBareProfileAsync(string fileName, string content = """{ "name": "Bare" }""")
    {
        var path = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    // ── DetectAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_ProfileWithEmbeddedTriggers_IsReported()
    {
        var path = await WriteProfileWithTriggersAsync("DCS.json", exeNames: "DCS.exe");

        var result = await _sut.DetectAsync();

        result.Should().ContainSingle().Which.Should().Be(path);
    }

    [Fact]
    public async Task DetectAsync_ProfilesWithoutTriggers_AreNotReported()
    {
        await WriteBareProfileAsync("NoProperty.json");
        await WriteBareProfileAsync("EmptyArray.json", """{ "name": "E", "autoLoadTriggers": [] }""");

        var result = await _sut.DetectAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_BrokenJson_IsSkipped()
    {
        await WriteBareProfileAsync("Broken.json", "{ not json }");
        var valid = await WriteProfileWithTriggersAsync("Valid.json", exeNames: "game.exe");

        var result = await _sut.DetectAsync();

        result.Should().ContainSingle().Which.Should().Be(valid);
    }

    [Fact]
    public async Task DetectAsync_MissingProfilesFolder_ReturnsEmpty()
    {
        Directory.Delete(_tempDir, recursive: true);
        Directory.CreateDirectory(_tempDir); // recreated empty for Dispose

        var result = await _sut.DetectAsync();

        result.Should().BeEmpty();
    }

    // ── MigrateAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task MigrateAsync_LiftsTriggersIntoGlobalList_WithProfilePath()
    {
        var path = await WriteProfileWithTriggersAsync("DCS.json", exeNames: "DCS.exe");

        var result = await _sut.MigrateAsync();

        result.MigratedProfileCount.Should().Be(1);
        result.TriggerCount.Should().Be(1);
        result.Failures.Should().BeEmpty();

        var trigger = _settings.AutoLoadTriggers.Should().ContainSingle().Subject;
        trigger.ProfilePath.Should().Be(path);
        trigger.MatchType.Should().Be(ProcessMatchType.ExecutableName);
        trigger.ExecutableName.Should().Be("DCS.exe");
        trigger.IsEnabled.Should().BeTrue();
        trigger.AutoStart.Should().BeTrue();
        trigger.RemainActiveOnFocusLoss.Should().BeFalse();

        _settingsMock.Verify(s => s.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MigrateAsync_StripsTriggersFromProfileFile_PreservingOtherContent()
    {
        var path = await WriteProfileWithTriggersAsync("DCS.json", exeNames: "DCS.exe");

        await _sut.MigrateAsync();

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        root.ContainsKey("autoLoadTriggers").Should().BeFalse();
        root["name"]!.GetValue<string>().Should().Be("DCS");
        root.ContainsKey("bindings").Should().BeTrue();
    }

    [Fact]
    public async Task MigrateAsync_SecondRun_IsIdempotent()
    {
        await WriteProfileWithTriggersAsync("DCS.json", exeNames: "DCS.exe");

        await _sut.MigrateAsync();
        var second = await _sut.MigrateAsync();

        second.MigratedProfileCount.Should().Be(0);
        second.TriggerCount.Should().Be(0);
        _settings.AutoLoadTriggers.Should().HaveCount(1);
        (await _sut.DetectAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task MigrateAsync_PreservesScanOrder_RootBeforeCategory()
    {
        // Scan order: root-level files first (alphabetical), then category subfolders.
        await WriteProfileWithTriggersAsync("Zulu.json", exeNames: "zulu.exe");
        await WriteProfileWithTriggersAsync("Alpha.json", exeNames: "alpha.exe");
        await WriteProfileWithTriggersAsync("Cat.json", category: "Racing", exeNames: "cat.exe");

        await _sut.MigrateAsync();

        _settings.AutoLoadTriggers.Select(t => t.ExecutableName)
            .Should().ContainInOrder("alpha.exe", "zulu.exe", "cat.exe");
    }

    [Fact]
    public async Task MigrateAsync_PreservesTriggerOrderWithinProfile()
    {
        await WriteProfileWithTriggersAsync("Multi.json", exeNames: ["first.exe", "second.exe"]);

        await _sut.MigrateAsync();

        _settings.AutoLoadTriggers.Select(t => t.ExecutableName)
            .Should().ContainInOrder("first.exe", "second.exe");
    }

    [Fact]
    public async Task MigrateAsync_BrokenJsonFile_ReportedAsFailure_OthersStillMigrated()
    {
        var broken = await WriteBareProfileAsync("Broken.json", "{ not json }");
        await WriteProfileWithTriggersAsync("Valid.json", exeNames: "game.exe");

        var result = await _sut.MigrateAsync();

        result.MigratedProfileCount.Should().Be(1);
        result.Failures.Should().ContainSingle().Which.ProfilePath.Should().Be(broken);
        _settings.AutoLoadTriggers.Should().ContainSingle()
            .Which.ExecutableName.Should().Be("game.exe");
    }

    [Fact]
    public async Task MigrateAsync_TriggerAlreadyInGlobalList_IsNotDuplicated()
    {
        // A previous run lifted the trigger but failed to strip the profile file.
        var path = await WriteProfileWithTriggersAsync("DCS.json", exeNames: "DCS.exe");
        _settings.AutoLoadTriggers =
        [
            new AutoLoadTrigger
            {
                ProfilePath    = path,
                MatchType      = ProcessMatchType.ExecutableName,
                ExecutableName = "DCS.exe",
                ExecutablePath = "C:/Games/DCS.exe",
            },
        ];

        var result = await _sut.MigrateAsync();

        result.TriggerCount.Should().Be(0);
        _settings.AutoLoadTriggers.Should().HaveCount(1);
        // The stale embedded copy is still stripped from the file.
        (await _sut.DetectAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task MigrateAsync_SettingsSaveFails_ProfileFilesAreUntouched()
    {
        var path = await WriteProfileWithTriggersAsync("DCS.json", exeNames: "DCS.exe");
        var before = await File.ReadAllTextAsync(path);
        _settingsMock.Setup(s => s.SaveAsync(It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new IOException("disk full"));

        var act = async () => await _sut.MigrateAsync();

        await act.Should().ThrowAsync<IOException>();
        (await File.ReadAllTextAsync(path)).Should().Be(before);
    }

    [Fact]
    public async Task MigrateAsync_NothingToMigrate_DoesNotSaveSettings()
    {
        await WriteBareProfileAsync("Bare.json");

        var result = await _sut.MigrateAsync();

        result.MigratedProfileCount.Should().Be(0);
        _settingsMock.Verify(s => s.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
