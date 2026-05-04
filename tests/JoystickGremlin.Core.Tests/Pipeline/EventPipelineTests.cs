// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.EmuWheel;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Pipeline;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging.Abstractions;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Tests.Pipeline;

public sealed class EventPipelineTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ProfileModel MakeProfile(Guid deviceGuid, string actionTag)
    {
        var profile = new ProfileModel { Name = "Test" };
        profile.Bindings.Add(new InputBinding
        {
            DeviceGuid = deviceGuid,
            InputType  = InputType.JoystickButton,
            Identifier = 1,
            Actions    = [new BoundAction { ActionTag = actionTag }],
        });
        return profile;
    }

    private static (EventPipeline pipeline, FakeDeviceManager deviceMgr, Mock<IActionRegistry> registry, ProfileState profileState)
        CreateSut()
    {
        var deviceMgr      = new FakeDeviceManager();
        var profileState   = new ProfileState();
        var registry       = new Mock<IActionRegistry>();
        var settingsSvc    = new Mock<ISettingsService>();
        settingsSvc.SetupGet(s => s.Settings).Returns(new AppSettings());
        var emuWheelMgr    = new NullEmuWheelDeviceManager();

        var pipeline = new EventPipeline(
            deviceMgr,
            registry.Object,
            profileState,
            settingsSvc.Object,
            emuWheelMgr,
            NullLogger<EventPipeline>.Instance);

        return (pipeline, deviceMgr, registry, profileState);
    }

    // ── IsRunning ──────────────────────────────────────────────────────────

    [Fact]
    public void IsRunning_BeforeStart_IsFalse()
    {
        var (pipeline, _, _, _) = CreateSut();
        pipeline.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void IsRunning_AfterStart_IsTrue()
    {
        var (pipeline, _, _, _) = CreateSut();
        pipeline.Start(new ProfileModel());
        pipeline.IsRunning.Should().BeTrue();
        pipeline.Stop();
    }

    [Fact]
    public void IsRunning_AfterStop_IsFalse()
    {
        var (pipeline, _, _, _) = CreateSut();
        pipeline.Start(new ProfileModel());
        pipeline.Stop();
        pipeline.IsRunning.Should().BeFalse();
    }

    // ── Dispatch ───────────────────────────────────────────────────────────

    [Fact]
    public async Task InputReceived_MatchingBinding_DispatchesFunctor()
    {
        var deviceGuid = Guid.NewGuid();
        var (pipeline, deviceMgr, registry, _) = CreateSut();

        var executed    = new TaskCompletionSource<InputEvent>();
        var fakeFunctor = new FakeFunctor(ev => executed.SetResult(ev));
        var descriptor  = new FakeDescriptor("my-action", fakeFunctor);
        registry.Setup(r => r.Resolve("my-action")).Returns(descriptor);

        var profile = MakeProfile(deviceGuid, "my-action");
        pipeline.Start(profile);

        deviceMgr.RaiseInput(new InputEvent(InputType.JoystickButton, deviceGuid, 1, 1.0));

        var received = await executed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        received.Identifier.Should().Be(1);
        pipeline.Stop();
    }

    [Fact]
    public async Task InputReceived_NoMatchingBinding_DoesNotDispatch()
    {
        var deviceGuid = Guid.NewGuid();
        var (pipeline, deviceMgr, registry, _) = CreateSut();

        var dispatched = false;
        registry.Setup(r => r.Resolve(It.IsAny<string>()))
                .Callback(() => dispatched = true)
                .Returns((IActionDescriptor?)null);

        var profile = MakeProfile(deviceGuid, "my-action");
        pipeline.Start(profile);

        // Wrong button index (2, not 1)
        deviceMgr.RaiseInput(new InputEvent(InputType.JoystickButton, deviceGuid, 2, 1.0));

        await Task.Delay(50);
        dispatched.Should().BeFalse();
        pipeline.Stop();
    }

    // ── Functor caching ────────────────────────────────────────────────────

    [Fact]
    public async Task InputReceived_SameBinding_ReusesFunctorAcrossEvents()
    {
        // Verifies that the same functor instance is reused so stateful functors
        // (e.g. Toggle) retain state between events.
        var deviceGuid = Guid.NewGuid();
        var (pipeline, deviceMgr, registry, _) = CreateSut();

        int createCount      = 0;
        var executedEvents   = new List<InputEvent>();

        // Descriptor creates a new stateful functor each time CreateFunctor is called.
        // With caching the pipeline must call it only once for a given BoundAction.
        var descriptorMock = new Mock<IActionDescriptor>();
        descriptorMock.SetupGet(d => d.Tag).Returns("counted-action");
        descriptorMock.Setup(d => d.CreateFunctor(It.IsAny<JsonObject?>()))
            .Returns(() =>
            {
                createCount++;
                return new FakeFunctor(ev => { lock (executedEvents) { executedEvents.Add(ev); } });
            });
        registry.Setup(r => r.Resolve("counted-action")).Returns(descriptorMock.Object);

        var profile = MakeProfile(deviceGuid, "counted-action");
        pipeline.Start(profile);

        // Fire the same binding three times.
        for (var i = 0; i < 3; i++)
            deviceMgr.RaiseInput(new InputEvent(InputType.JoystickButton, deviceGuid, 1, 1.0));

        await Task.Delay(150);

        createCount.Should().Be(1, "functor should be created once and cached");
        executedEvents.Should().HaveCount(3, "functor should be executed for every event");
        pipeline.Stop();
    }

    [Fact]
    public void Stop_ClearsFunctorCache_SoRestartCreatesNewFunctors()
    {
        var deviceGuid = Guid.NewGuid();
        var (pipeline, _, registry, _) = CreateSut();

        int createCount    = 0;
        var descriptorMock = new Mock<IActionDescriptor>();
        descriptorMock.SetupGet(d => d.Tag).Returns("cached-action");
        descriptorMock.Setup(d => d.CreateFunctor(It.IsAny<JsonObject?>()))
            .Returns(() => { createCount++; return new FakeFunctor(_ => { }); });
        registry.Setup(r => r.Resolve("cached-action")).Returns(descriptorMock.Object);

        var profile = MakeProfile(deviceGuid, "cached-action");

        // First run
        pipeline.Start(profile);
        pipeline.Stop();

        // Second run with same profile — cache was cleared, so functor is created fresh.
        pipeline.Start(profile);
        pipeline.Stop();

        createCount.Should().Be(0, "functor is created lazily on first event, not on start");
    }

    // ── Dispose ────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_WhenRunning_StopsAndUnsubscribes()
    {
        var (pipeline, _, _, _) = CreateSut();
        pipeline.Start(new ProfileModel());
        pipeline.Dispose();
        pipeline.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ProfileModified_WhileRunning_ClearsFunctorCache_AndRecreatesFunctor()
    {
        var deviceGuid = Guid.NewGuid();
        var (pipeline, deviceMgr, registry, profileState) = CreateSut();

        int createCount    = 0;
        var descriptorMock = new Mock<IActionDescriptor>();
        descriptorMock.SetupGet(d => d.Tag).Returns("refresh-action");
        descriptorMock.Setup(d => d.CreateFunctor(It.IsAny<JsonObject?>()))
            .Returns(() =>
            {
                createCount++;
                return new FakeFunctor(_ => { });
            });
        registry.Setup(r => r.Resolve("refresh-action")).Returns(descriptorMock.Object);

        var profile = MakeProfile(deviceGuid, "refresh-action");
        profileState.SetProfile(profile);
        pipeline.Start(profile);

        deviceMgr.RaiseInput(new InputEvent(InputType.JoystickButton, deviceGuid, 1, 1.0));
        await Task.Delay(50);

        profileState.NotifyProfileModified();

        deviceMgr.RaiseInput(new InputEvent(InputType.JoystickButton, deviceGuid, 1, 1.0));
        await Task.Delay(50);

        createCount.Should().Be(2, "profile edits while running must invalidate cached functors");
        pipeline.Stop();
    }

    // ── Test doubles ───────────────────────────────────────────────────────

    private sealed class FakeDeviceManager : IDeviceManager
    {
#pragma warning disable CS0067
        public event EventHandler<IPhysicalDevice>? DeviceConnected;
        public event EventHandler<IPhysicalDevice>? DeviceDisconnected;
#pragma warning restore CS0067
        public event EventHandler<InputEvent>? InputReceived;

        public IReadOnlyList<IPhysicalDevice> Devices => [];

        public void Initialize() { }
        public void Dispose() { }

        public void RaiseInput(InputEvent ev) => InputReceived?.Invoke(this, ev);
    }

    private sealed class FakeDescriptor(string tag, IActionFunctor functor) : IActionDescriptor
    {
        public string Tag { get; } = tag;
        public string Name => Tag;
        public IActionFunctor CreateFunctor(JsonObject? configuration) => functor;
    }

    private sealed class FakeFunctor(Action<InputEvent> callback) : IActionFunctor
    {
        public Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default)
        {
            callback(inputEvent);
            return Task.CompletedTask;
        }
    }
}

