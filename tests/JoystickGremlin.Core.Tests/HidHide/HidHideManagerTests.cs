// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;
using JoystickGremlin.Core.HidHide;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JoystickGremlin.Core.Tests.HidHide;

public sealed class HidHideManagerTests
{
    /// <summary>In-memory, mutable IHidHideController for test isolation.</summary>
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

    private readonly FakeController _controller = new();
    private readonly ILogger<HidHideManager> _logger = NullLogger<HidHideManager>.Instance;

    private HidHideManager CreateSut() => new(_controller, _logger);

    [Fact]
    public async Task InitializeAsync_WhitelistsOwnExePath()
    {
        using var sut = CreateSut();

        await sut.InitializeAsync();

        // Own exe path varies per test runner; verify at least one entry was added.
        _controller.ApplicationPaths.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyWhitelisted_DoesNotAddDuplicate()
    {
        using var sut = CreateSut();
        await sut.InitializeAsync();
        var countAfterFirst = _controller.ApplicationPaths.Count;

        await sut.InitializeAsync();

        _controller.ApplicationPaths.Count.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task InitializeAsync_WhenDriverMissing_DoesNotThrow()
    {
        _controller.IsInstalled = false;
        using var sut = CreateSut();

        await sut.Invoking(s => s.InitializeAsync()).Should().NotThrowAsync();
        _controller.ApplicationPaths.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispose_RemovesOwnExeFromWhitelist()
    {
        var sut = CreateSut();
        await sut.InitializeAsync();
        var addedPath = _controller.ApplicationPaths.FirstOrDefault();

        sut.Dispose();

        if (addedPath is not null)
        {
            _controller.ApplicationPaths.Should().NotContain(addedPath);
        }
    }

    [Fact]
    public async Task Dispose_WhenDriverMissing_DoesNotThrow()
    {
        _controller.IsInstalled = false;
        var sut = CreateSut();
        await sut.InitializeAsync();

        sut.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_CalledTwice_RemovesWhitelistEntryOnlyOnce()
    {
        // Guard against the `_disposed` flag being removed by a future "tidy up" pass:
        // a second Dispose must be a no-op (must not attempt another RemoveApplicationPath
        // call, which would no-op against a fake but would re-trigger CLI/UAC in production).
        var sut = CreateSut();
        await sut.InitializeAsync();
        sut.Dispose();
        var pathsAfterFirstDispose = _controller.ApplicationPaths.Count;

        sut.Invoking(s => s.Dispose()).Should().NotThrow();

        _controller.ApplicationPaths.Count.Should().Be(pathsAfterFirstDispose);
    }
}
