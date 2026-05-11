// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentAssertions;
using JoystickGremlin.Core.Profile;

namespace JoystickGremlin.Core.Tests.ProfileBackend;

public sealed class ProfileBackendSerializationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IProfileRepository _sut = new ProfileRepository();
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ProfileBackendSerializationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jg_backend_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task Save_ProfileWithoutBackend_OmitsPreferredBackendIdFromJson()
    {
        var path = Path.Combine(_tempDir, "no-backend.json");
        var profile = new global::JoystickGremlin.Core.Profile.Profile { Name = "Test" };

        await _sut.SaveAsync(profile, path);

        var json = await File.ReadAllTextAsync(path);
        json.Should().NotContain("PreferredBackendId", "null backend id must not be serialized for legacy compatibility");
        json.Should().NotContain("preferredBackendId");
    }

    [Fact]
    public async Task Save_ProfileWithBackend_PersistsPreferredBackendId()
    {
        var path = Path.Combine(_tempDir, "with-backend.json");
        var profile = new global::JoystickGremlin.Core.Profile.Profile { Name = "Test", PreferredBackendId = "jgs-wheel" };

        await _sut.SaveAsync(profile, path);

        var node = JsonNode.Parse(await File.ReadAllTextAsync(path));
        var backendIdToken = node!["PreferredBackendId"] ?? node!["preferredBackendId"];
        backendIdToken!.GetValue<string>().Should().Be("jgs-wheel");
    }

    [Fact]
    public async Task Load_ProfileWithoutBackendField_DefaultsToNull()
    {
        var path = Path.Combine(_tempDir, "legacy.json");
        await File.WriteAllTextAsync(path, """
            { "name": "Legacy", "bindings": [] }
            """);

        var loaded = await _sut.LoadAsync(path);

        loaded.PreferredBackendId.Should().BeNull();
        loaded.Name.Should().Be("Legacy");
    }

    [Fact]
    public async Task RoundTrip_PreservesPreferredBackendId()
    {
        var path = Path.Combine(_tempDir, "roundtrip.json");
        var original = new global::JoystickGremlin.Core.Profile.Profile { Name = "RT", PreferredBackendId = "vjoy" };

        await _sut.SaveAsync(original, path);
        var loaded = await _sut.LoadAsync(path);

        loaded.PreferredBackendId.Should().Be("vjoy");
    }
}
