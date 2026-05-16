// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Exceptions;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging.Abstractions;

namespace JoystickGremlin.Core.Tests.Profile;

public sealed class ProfileLibraryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProfileLibrary _sut;
    private readonly Mock<ISettingsService> _settingsMock;
    private readonly IProfileRepository _repo;

    public ProfileLibraryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jg_lib_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _repo = new ProfileRepository();
        _settingsMock = new Mock<ISettingsService>();
        _settingsMock.SetupGet(s => s.Settings)
                     .Returns(new AppSettings { ProfilesFolderPath = _tempDir });

        _sut = new ProfileLibrary(
            _settingsMock.Object,
            _repo,
            NullLogger<ProfileLibrary>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        Directory.Delete(_tempDir, recursive: true);
    }

    // ── ScanAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ScanAsync_EmptyFolder_ReturnsNoEntries()
    {
        await _sut.ScanAsync();

        _sut.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_RootLevelProfiles_ReturnedWithNullCategory()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Alpha.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Beta.json"), "{}");

        await _sut.ScanAsync();

        _sut.Entries.Should().HaveCount(2);
        _sut.Entries.Should().AllSatisfy(e => e.Category.Should().BeNull());
        _sut.Entries.Select(e => e.Name).Should().Contain(["Alpha", "Beta"]);
    }

    [Fact]
    public async Task ScanAsync_SubfolderProfiles_ReturnedWithCategory()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir, "Racing")).FullName;
        await File.WriteAllTextAsync(Path.Combine(sub, "F1.json"), "{}");

        await _sut.ScanAsync();

        _sut.Entries.Should().HaveCount(1);
        _sut.Entries[0].Name.Should().Be("F1");
        _sut.Entries[0].Category.Should().Be("Racing");
    }

    [Fact]
    public async Task ScanAsync_NonJsonFilesIgnored()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "readme.txt"), "hello");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "valid.json"), "{}");

        await _sut.ScanAsync();

        _sut.Entries.Should().HaveCount(1);
        _sut.Entries[0].Name.Should().Be("valid");
    }

    // ── CreateProfileAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateProfileAsync_CreatesFileAndScans()
    {
        var path = await _sut.CreateProfileAsync("My Profile");

        File.Exists(path).Should().BeTrue();
        _sut.Entries.Should().HaveCount(1);
        _sut.Entries[0].Name.Should().Be("My Profile");
    }

    [Fact]
    public async Task CreateProfileAsync_WithCategory_CreatesInSubfolder()
    {
        var path = await _sut.CreateProfileAsync("Oval", "NASCAR");

        Path.GetDirectoryName(path)!.Should().EndWith("NASCAR");
        _sut.Entries.Should().HaveCount(1);
        _sut.Entries[0].Category.Should().Be("NASCAR");
    }

    [Fact]
    public async Task CreateProfileAsync_DuplicateName_ThrowsProfileException()
    {
        await _sut.CreateProfileAsync("Dupe");

        var act = async () => await _sut.CreateProfileAsync("Dupe");

        await act.Should().ThrowAsync<ProfileException>();
    }

    // ── EmptyCategories / CreateCategoryAsync ──────────────────────────────

    [Fact]
    public async Task ScanAsync_EmptySubfolder_IsListedInEmptyCategories()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Drifting"));

        await _sut.ScanAsync();

        _sut.Entries.Should().BeEmpty();
        _sut.EmptyCategories.Should().Contain("Drifting");
    }

    [Fact]
    public async Task ScanAsync_SubfolderWithProfile_NotListedAsEmpty()
    {
        var sub = Path.Combine(_tempDir, "Rally");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "Gravel.json"), "{}");

        await _sut.ScanAsync();

        _sut.EmptyCategories.Should().NotContain("Rally");
        _sut.Entries.Should().ContainSingle(e => e.Category == "Rally");
    }

    [Fact]
    public async Task CreateCategoryAsync_NewName_CreatesFolderAndAppearsInEmptyCategories()
    {
        await _sut.CreateCategoryAsync("Touring");

        Directory.Exists(Path.Combine(_tempDir, "Touring")).Should().BeTrue();
        _sut.EmptyCategories.Should().Contain("Touring");
    }

    [Fact]
    public async Task CreateCategoryAsync_ExistingFolder_ThrowsProfileException()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Already"));

        var act = async () => await _sut.CreateCategoryAsync("Already");

        await act.Should().ThrowAsync<ProfileException>();
    }

    [Fact]
    public async Task CreateProfileAsync_IntoExistingEmptyCategory_RemovesItFromEmptyList()
    {
        await _sut.CreateCategoryAsync("Karts");

        await _sut.CreateProfileAsync("First", "Karts");

        _sut.EmptyCategories.Should().NotContain("Karts");
        _sut.Entries.Should().ContainSingle(e => e.Category == "Karts" && e.Name == "First");
    }

    // ── DeleteProfileAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProfileAsync_ExistingFile_RemovesAndScans()
    {
        var path = await _sut.CreateProfileAsync("ToDelete");

        await _sut.DeleteProfileAsync(path);

        File.Exists(path).Should().BeFalse();
        _sut.Entries.Should().BeEmpty();
    }

    // ── RenameProfileAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task RenameProfileAsync_ValidRename_RenamesFileAndScans()
    {
        var path = await _sut.CreateProfileAsync("Old");

        await _sut.RenameProfileAsync(path, "New");

        _sut.Entries.Should().HaveCount(1);
        _sut.Entries[0].Name.Should().Be("New");
    }

    [Fact]
    public async Task RenameProfileAsync_NonExistentFile_ThrowsProfileException()
    {
        var fakePath = Path.Combine(_tempDir, "ghost.json");

        var act = async () => await _sut.RenameProfileAsync(fakePath, "Whatever");

        await act.Should().ThrowAsync<ProfileException>();
    }

    // ── LibraryChanged event ───────────────────────────────────────────────

    [Fact]
    public async Task ScanAsync_RaisesLibraryChanged()
    {
        var eventFired = false;
        _sut.LibraryChanged += (_, _) => eventFired = true;

        await _sut.ScanAsync();

        eventFired.Should().BeTrue();
    }
}
