// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Events;

namespace JoystickGremlin.Core.Tests.Actions;

public sealed class ActionRegistryTests
{
    private readonly IActionRegistry _sut = new ActionRegistry();

    // ── Register + Resolve ─────────────────────────────────────────────────

    [Fact]
    public void Register_ThenResolve_ReturnsSameDescriptor()
    {
        var descriptor = new FakeActionDescriptor("test-action");

        _sut.Register(descriptor);

        var resolved = _sut.Resolve("test-action");
        resolved.Should().BeSameAs(descriptor);
    }

    [Fact]
    public void Resolve_UnknownTag_ReturnsNull()
    {
        var result = _sut.Resolve("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void Register_DuplicateTag_ReplacesExisting()
    {
        var first = new FakeActionDescriptor("my-action", "First");
        var second = new FakeActionDescriptor("my-action", "Second");

        _sut.Register(first);
        _sut.Register(second);

        _sut.Resolve("my-action")!.Name.Should().Be("Second");
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_MultipleRegistrations_ReturnsAllDescriptors()
    {
        _sut.Register(new FakeActionDescriptor("action-a"));
        _sut.Register(new FakeActionDescriptor("action-b"));
        _sut.Register(new FakeActionDescriptor("action-c"));

        var all = _sut.GetAll();

        all.Should().HaveCount(3);
        all.Select(d => d.Tag).Should().BeEquivalentTo("action-a", "action-b", "action-c");
    }

    [Fact]
    public void GetAll_EmptyRegistry_ReturnsEmptyList()
    {
        _sut.GetAll().Should().BeEmpty();
    }

    // ── CreateFunctor ──────────────────────────────────────────────────────

    [Fact]
    public void Descriptor_CreateFunctor_ReturnsFunctorInstance()
    {
        var descriptor = new FakeActionDescriptor("functor-test");
        _sut.Register(descriptor);

        var functor = _sut.Resolve("functor-test")!.CreateFunctor(null);

        functor.Should().NotBeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private sealed class FakeActionDescriptor(string tag, string name = "Fake") : IActionDescriptor
    {
        public string Tag { get; } = tag;
        public string Name { get; } = name;
        public IActionFunctor CreateFunctor(JsonObject? configuration) => new FakeFunctor();
    }

    private sealed class FakeFunctor : IActionFunctor
    {
        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
