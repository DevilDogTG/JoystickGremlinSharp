// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Exceptions;
using JoystickGremlin.Core.Profile;

namespace JoystickGremlin.Core.Tests.Profile;

public sealed class ProfileRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IProfileRepository _sut;

    public ProfileRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jg_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sut = new ProfileRepository();
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── LoadOrCreateAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadOrCreate_NonExistentFile_ReturnsNewProfileWithNameFromFileName()
    {
        var path = Path.Combine(_tempDir, "MyProfile.json");

        var profile = await _sut.LoadOrCreateAsync(path);

        profile.Should().NotBeNull();
        profile.Name.Should().Be("MyProfile");
        profile.Bindings.Should().BeEmpty();
    }

    // ── Save → Load round-trip ─────────────────────────────────────────────

    [Fact]
    public async Task SaveThenLoad_SimpleProfile_RoundTripsCorrectly()
    {
        var path = Path.Combine(_tempDir, "simple.json");
        var original = new JoystickGremlin.Core.Profile.Profile
        {
            Name = "Test Profile",
            Bindings =
            [
                new InputBinding
                {
                    DeviceGuid = Guid.NewGuid(),
                    InputType  = InputType.JoystickButton,
                    Identifier = 1,
                },
            ],
        };

        await _sut.SaveAsync(original, path);
        var loaded = await _sut.LoadAsync(path);

        loaded.Id.Should().Be(original.Id);
        loaded.Name.Should().Be("Test Profile");
        loaded.Bindings.Should().HaveCount(1);
        loaded.Bindings[0].InputType.Should().Be(InputType.JoystickButton);
        loaded.Bindings[0].Identifier.Should().Be(1);
    }

    [Fact]
    public async Task SaveThenLoad_BoundActionWithJsonConfig_RoundTripsConfiguration()
    {
        var path   = Path.Combine(_tempDir, "actions.json");
        var config = new JsonObject { ["vjoyId"] = 1, ["axisIndex"] = 2 };
        var original = new JoystickGremlin.Core.Profile.Profile
        {
            Name = "ActionProfile",
            Bindings =
            [
                new InputBinding
                {
                    DeviceGuid = Guid.Empty,
                    InputType  = InputType.JoystickAxis,
                    Identifier = 0,
                    Actions    = [new BoundAction { ActionTag = "vjoy-axis", Configuration = config }],
                },
            ],
        };

        await _sut.SaveAsync(original, path);
        var loaded = await _sut.LoadAsync(path);

        var action = loaded.Bindings[0].Actions[0];
        action.ActionTag.Should().Be("vjoy-axis");
        action.Configuration.Should().NotBeNull();
        action.Configuration!["vjoyId"]!.GetValue<int>().Should().Be(1);
        action.Configuration["axisIndex"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoriesIfMissing()
    {
        var path    = Path.Combine(_tempDir, "sub", "nested", "profile.json");
        var profile = new JoystickGremlin.Core.Profile.Profile { Name = "Nested" };

        await _sut.SaveAsync(profile, path);

        File.Exists(path).Should().BeTrue();
    }

    // ── Error cases ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NonExistentFile_ThrowsProfileException()
    {
        var path = Path.Combine(_tempDir, "missing.json");

        var act = async () => await _sut.LoadAsync(path);

        await act.Should().ThrowAsync<ProfileException>();
    }

    [Fact]
    public async Task LoadAsync_CorruptJson_ThrowsProfileException()
    {
        var path = Path.Combine(_tempDir, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json }}");

        var act = async () => await _sut.LoadAsync(path);

        await act.Should().ThrowAsync<ProfileException>();
    }

    // ── Legacy migration ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_LegacyModesFormat_MigratesFirstModesBindings()
    {
        var path = Path.Combine(_tempDir, "legacy.json");
        var deviceGuid = Guid.NewGuid();
        var legacyJson = $$"""
            {
              "id": "00000000-0000-0000-0000-000000000001",
              "name": "Legacy",
              "modes": [
                {
                  "name": "Default",
                  "bindings": [
                    {
                      "deviceGuid": "{{deviceGuid}}",
                      "inputType": "JoystickButton",
                      "identifier": 3,
                      "actions": [
                        { "actionTag": "vjoy-button", "configuration": { "vjoyId": 1, "buttonIndex": 5 } }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(path, legacyJson);

        var profile = await _sut.LoadAsync(path);

        profile.Name.Should().Be("Legacy");
        profile.Bindings.Should().HaveCount(1);
        profile.Bindings[0].DeviceGuid.Should().Be(deviceGuid);
        profile.Bindings[0].Identifier.Should().Be(3);
        profile.Bindings[0].Actions[0].ActionTag.Should().Be("vjoy-button");
    }

    [Fact]
    public async Task LoadAsync_LegacyFormat_ChangeModeActionsAreDropped()
    {
        var path = Path.Combine(_tempDir, "legacy_cm.json");
        var legacyJson = """
            {
              "name": "LegacyCM",
              "modes": [
                {
                  "name": "Default",
                  "bindings": [
                    {
                      "deviceGuid": "00000000-0000-0000-0000-000000000000",
                      "inputType": "JoystickButton",
                      "identifier": 1,
                      "actions": [
                        { "actionTag": "change-mode", "configuration": { "targetMode": "Combat" } },
                        { "actionTag": "vjoy-button", "configuration": { "vjoyId": 1, "buttonIndex": 1 } }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(path, legacyJson);

        var profile = await _sut.LoadAsync(path);

        var actions = profile.Bindings[0].Actions;
        actions.Should().HaveCount(1);
        actions[0].ActionTag.Should().Be("vjoy-button");
    }
}

