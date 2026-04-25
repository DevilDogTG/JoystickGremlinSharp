// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Profile;

namespace JoystickGremlin.Core.Tests.Profile;

public sealed class ProfileStateTests
{
    private readonly IProfileState _sut = new ProfileState();

    // ── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsNull()
    {
        _sut.CurrentProfile.Should().BeNull();
        _sut.FilePath.Should().BeNull();
    }

    // ── SetProfile ───────────────────────────────────────────────────────────

    [Fact]
    public void SetProfile_SetsCurrentProfileAndFilePath()
    {
        var profile = new JoystickGremlin.Core.Profile.Profile { Name = "Test" };

        _sut.SetProfile(profile, "/path/to/profile.json");

        _sut.CurrentProfile.Should().BeSameAs(profile);
        _sut.FilePath.Should().Be("/path/to/profile.json");
    }

    [Fact]
    public void SetProfile_RaisesProfileChangedEvent()
    {
        JoystickGremlin.Core.Profile.Profile? received = null;
        _sut.ProfileChanged += (_, p) => received = p;

        var profile = new JoystickGremlin.Core.Profile.Profile { Name = "Test" };
        _sut.SetProfile(profile);

        received.Should().BeSameAs(profile);
    }

    [Fact]
    public void SetProfile_RaisesFilePathChangedEvent()
    {
        string? receivedPath = null;
        _sut.FilePathChanged += (_, p) => receivedPath = p;

        var profile = new JoystickGremlin.Core.Profile.Profile { Name = "Test" };
        _sut.SetProfile(profile, "/some/path.json");

        receivedPath.Should().Be("/some/path.json");
    }

    [Fact]
    public void SetProfile_SameFilePath_DoesNotRaiseFilePathChangedAgain()
    {
        var profile = new JoystickGremlin.Core.Profile.Profile { Name = "Test" };
        _sut.SetProfile(profile, "/path.json");

        var callCount = 0;
        _sut.FilePathChanged += (_, _) => callCount++;

        _sut.SetProfile(profile, "/path.json"); // same path → no extra event

        callCount.Should().Be(0);
    }

    // ── UpdateFilePath ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateFilePath_ChangesPath_AndRaisesEvent()
    {
        string? received = null;
        _sut.FilePathChanged += (_, p) => received = p;

        _sut.UpdateFilePath("/new/path.json");

        _sut.FilePath.Should().Be("/new/path.json");
        received.Should().Be("/new/path.json");
    }

    // ── NotifyProfileModified ────────────────────────────────────────────────

    [Fact]
    public void NotifyProfileModified_RaisesProfileChangedWithCurrentProfile()
    {
        var profile = new JoystickGremlin.Core.Profile.Profile { Name = "Test" };
        _sut.SetProfile(profile);

        JoystickGremlin.Core.Profile.Profile? received = null;
        _sut.ProfileChanged += (_, p) => received = p;

        _sut.NotifyProfileModified();

        received.Should().BeSameAs(profile);
    }

    // ── ClearProfile ─────────────────────────────────────────────────────────

    [Fact]
    public void ClearProfile_SetsNullAndRaisesBothEvents()
    {
        var profile = new JoystickGremlin.Core.Profile.Profile { Name = "Test" };
        _sut.SetProfile(profile, "/path.json");

        JoystickGremlin.Core.Profile.Profile? profileEvent = new JoystickGremlin.Core.Profile.Profile { Name = "not null" };
        string? pathEvent = "not null";
        _sut.ProfileChanged += (_, p) => profileEvent = p;
        _sut.FilePathChanged += (_, p) => pathEvent = p;

        _sut.ClearProfile();

        _sut.CurrentProfile.Should().BeNull();
        _sut.FilePath.Should().BeNull();
        profileEvent.Should().BeNull();
        pathEvent.Should().BeNull();
    }
}
