// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.HidHide;
using JoystickGremlin.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Tests.HidHide;

public sealed class HidHideManagerTests : IDisposable
{
    // ── Fakes / Mocks ─────────────────────────────────────────────────────────

    /// <summary>In-memory, mutable IHidHideController implementation for test isolation.</summary>
    private sealed class FakeController : IHidHideController
    {
        public bool IsInstalled { get; set; } = true;
        public bool IsActive { get; set; }

        private readonly List<string> _blocked = [];
        private readonly List<string> _apps = [];

        public IReadOnlyList<string> BlockedInstanceIds => _blocked;
        public IReadOnlyList<string> ApplicationPaths => _apps;

        public void AddBlockedInstance(string instanceId) => _blocked.Add(instanceId);
        public void RemoveBlockedInstance(string instanceId) => _blocked.Remove(instanceId);
        public void AddApplicationPath(string fullPath) => _apps.Add(fullPath);
        public void RemoveApplicationPath(string fullPath) => _apps.Remove(fullPath);
        public void Refresh() { /* no-op */ }
    }

    /// <summary>Minimal IEventPipeline that lets tests fire Started/Stopped manually.</summary>
    private sealed class FakePipeline : IEventPipeline
    {
        public bool IsRunning { get; private set; }
        public event EventHandler? Started;
        public event EventHandler? Stopped;

        public void Start(ProfileModel profile)
        {
            IsRunning = true;
            Started?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            IsRunning = false;
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() { }
    }

    // ── Common setup ──────────────────────────────────────────────────────────

    private readonly FakeController _controller = new();
    private readonly FakePipeline _pipeline = new();
    private readonly Mock<ISettingsService> _settingsMock = new();
    private readonly Mock<ILogger<HidHideManager>> _loggerMock = new();

    private AppSettings _appSettings = new()
    {
        EnableHidHide = true,
        AutoHideOnPipelineRun = true,
        HiddenDeviceInstanceIds = ["HID\\VID_AAAA&PID_0001\\001"],
        HiddenDevices = [new HiddenDeviceEntry { InstanceId = "HID\\VID_AAAA&PID_0001\\001", FriendlyName = "Test Device" }]
    };

    private HidHideManager CreateSut()
    {
        _settingsMock.Setup(s => s.Settings).Returns(() => _appSettings);
        return new HidHideManager(_controller, _settingsMock.Object, _pipeline, _loggerMock.Object);
    }

    public void Dispose() { /* sut is disposed per test */ }

    // ── Initial status ────────────────────────────────────────────────────────

    [Fact]
    public void InitialStatus_WithDriverInstalled_IsReady()
    {
        using var sut = CreateSut();

        sut.Status.Should().Be(HidHideStatus.Ready);
    }

    [Fact]
    public void InitialStatus_DriverNotInstalled_IsNotInstalled()
    {
        _controller.IsInstalled = false;
        using var sut = CreateSut();

        sut.Status.Should().Be(HidHideStatus.NotInstalled);
    }

    [Fact]
    public void InitialStatus_FeatureDisabled_IsDisabled()
    {
        _appSettings = new AppSettings { EnableHidHide = false };
        using var sut = CreateSut();

        sut.Status.Should().Be(HidHideStatus.Disabled);
    }

    // ── ApplyAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_BlocksConfiguredDevices_AndSetsActive()
    {
        using var sut = CreateSut();

        await sut.ApplyAsync();

        _controller.BlockedInstanceIds.Should().Contain("HID\\VID_AAAA&PID_0001\\001");
        _controller.IsActive.Should().BeTrue();
        sut.IsApplied.Should().BeTrue();
        sut.Status.Should().Be(HidHideStatus.Active);
    }

    [Fact]
    public async Task ApplyAsync_WhitelistsOwnExePath()
    {
        using var sut = CreateSut();

        await sut.ApplyAsync();

        // Own exe path should be present in application paths.
        // (The exact path varies per test runner; we just verify at least one entry was added.)
        _controller.ApplicationPaths.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_WhenAlreadyApplied_IsIdempotent()
    {
        using var sut = CreateSut();
        await sut.ApplyAsync();
        var blockedCountAfterFirst = _controller.BlockedInstanceIds.Count;

        await sut.ApplyAsync();

        _controller.BlockedInstanceIds.Count.Should().Be(blockedCountAfterFirst,
            "second Apply must not double-add instance IDs");
    }

    [Fact]
    public async Task ApplyAsync_WhenDisabled_DoesNothing()
    {
        _appSettings = new AppSettings { EnableHidHide = false };
        using var sut = CreateSut();

        await sut.ApplyAsync();

        _controller.BlockedInstanceIds.Should().BeEmpty();
        _controller.IsActive.Should().BeFalse();
        sut.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_WhenDriverMissing_SetsNotInstalledStatus()
    {
        _controller.IsInstalled = false;
        using var sut = CreateSut();

        await sut.ApplyAsync();

        sut.IsApplied.Should().BeFalse();
        sut.Status.Should().Be(HidHideStatus.NotInstalled);
    }

    // ── RevertAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RevertAsync_RemovesBlockedDevices_AndClearsApplied()
    {
        using var sut = CreateSut();
        await sut.ApplyAsync();

        await sut.RevertAsync();

        _controller.BlockedInstanceIds.Should().NotContain("HID\\VID_AAAA&PID_0001\\001");
        sut.IsApplied.Should().BeFalse();
        sut.Status.Should().Be(HidHideStatus.Ready);
    }

    [Fact]
    public async Task RevertAsync_RetainsOwnExeInWhitelist()
    {
        // Own exe stays whitelisted through Revert — it is only removed on Dispose (clean exit).
        using var sut = CreateSut();
        await sut.ApplyAsync();
        var addedPath = _controller.ApplicationPaths.FirstOrDefault();

        await sut.RevertAsync();

        if (addedPath is not null)
            _controller.ApplicationPaths.Should().Contain(addedPath);
    }

    [Fact]
    public async Task Dispose_RemovesOwnExeFromWhitelist()
    {
        // Own exe is removed from the whitelist only on clean app exit (Dispose).
        var sut = CreateSut();
        await sut.ApplyAsync();
        var addedPath = _controller.ApplicationPaths.FirstOrDefault();

        sut.Dispose();

        if (addedPath is not null)
            _controller.ApplicationPaths.Should().NotContain(addedPath);
    }

    [Fact]
    public async Task RevertAsync_WhenNotApplied_IsIdempotent()
    {
        using var sut = CreateSut();

        // Should not throw — reverts are idempotent
        await sut.RevertAsync();

        sut.IsApplied.Should().BeFalse();
    }

    [Fact]
    public async Task RevertAsync_TurnsOffGate_WhenNoOtherBlockedDevices()
    {
        using var sut = CreateSut();
        await sut.ApplyAsync();

        await sut.RevertAsync();

        _controller.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RevertAsync_LeavesGateOn_WhenOtherDevicesStillBlocked()
    {
        // Add a device to the block list that we are NOT responsible for (foreign entry).
        _controller.AddBlockedInstance("HID\\VID_FFFF&PID_0001\\999");

        using var sut = CreateSut();
        await sut.ApplyAsync();

        await sut.RevertAsync();

        // Gate must remain on because "foreign" device is still blocked.
        _controller.IsActive.Should().BeTrue();
    }

    // ── Pipeline event coupling ───────────────────────────────────────────────

    [Fact]
    public async Task PipelineStart_TriggersApply_WhenAutoHideEnabled()
    {
        using var sut = CreateSut();

        _pipeline.Start(new ProfileModel());
        // Allow fire-and-forget task to complete.
        await Task.Delay(50);

        sut.IsApplied.Should().BeTrue();
        sut.Status.Should().Be(HidHideStatus.Active);
    }

    [Fact]
    public async Task PipelineStop_TriggersRevert_WhenAutoHideEnabled()
    {
        using var sut = CreateSut();
        _pipeline.Start(new ProfileModel());
        await Task.Delay(50);

        _pipeline.Stop();
        await Task.Delay(50);

        sut.IsApplied.Should().BeFalse();
        sut.Status.Should().Be(HidHideStatus.Ready);
    }

    [Fact]
    public async Task PipelineStart_DoesNotApply_WhenAutoHideDisabled()
    {
        _appSettings = new AppSettings
        {
            EnableHidHide = true,
            AutoHideOnPipelineRun = false,
            HiddenDeviceInstanceIds = ["HID\\VID_AAAA&PID_0001\\001"]
        };
        using var sut = CreateSut();

        _pipeline.Start(new ProfileModel());
        await Task.Delay(50);

        sut.IsApplied.Should().BeFalse();
    }

    // ── InitializeAsync (crash recovery) ─────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_RemovesStaleBlockedDevices()
    {
        // Simulate a previous-crash state: device was left in the block list.
        _controller.AddBlockedInstance("HID\\VID_AAAA&PID_0001\\001");
        using var sut = CreateSut();

        await sut.InitializeAsync();

        _controller.BlockedInstanceIds.Should().NotContain("HID\\VID_AAAA&PID_0001\\001");
    }

    [Fact]
    public async Task InitializeAsync_WhenDriverMissing_DoesNotThrow()
    {
        _controller.IsInstalled = false;
        using var sut = CreateSut();

        // Should not throw even if driver is absent
        await sut.Invoking(s => s.InitializeAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyWhitelisted_DoesNotAddDuplicate()
    {
        // If own exe is already in the whitelist, EnsureWhitelistedAsync must not
        // add a second entry — i.e., no unnecessary CLI write is performed.
        using var sut = CreateSut();
        await sut.InitializeAsync(); // first call — adds own exe

        var countAfterFirst = _controller.ApplicationPaths.Count;

        await sut.InitializeAsync(); // second call — should NOT add a duplicate

        _controller.ApplicationPaths.Count.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task ApplyAsync_WhenAlreadyBlocked_DoesNotAddDuplicate()
    {
        // If a device is already in the block list (e.g. from another tool), ApplyAsync
        // must not add a duplicate entry — i.e., no unnecessary CLI write is performed.
        _controller.AddBlockedInstance("HID\\VID_AAAA&PID_0001\\001");
        using var sut = CreateSut();

        await sut.ApplyAsync();

        _controller.BlockedInstanceIds.Count(id =>
            string.Equals(id, "HID\\VID_AAAA&PID_0001\\001", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
    }

    // ── StatusChanged event ───────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_RaisesStatusChanged_ToActive()
    {
        using var sut = CreateSut();
        var events = new List<HidHideStatus>();
        sut.StatusChanged += (_, _) => events.Add(sut.Status);

        await sut.ApplyAsync();

        events.Should().Contain(HidHideStatus.Active);
    }

    [Fact]
    public async Task RevertAsync_RaisesStatusChanged_ToReady()
    {
        using var sut = CreateSut();
        await sut.ApplyAsync();

        var events = new List<HidHideStatus>();
        sut.StatusChanged += (_, _) => events.Add(sut.Status);

        await sut.RevertAsync();

        events.Should().Contain(HidHideStatus.Ready);
    }
}
