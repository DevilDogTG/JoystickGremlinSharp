// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Devices page — shows connected physical input devices
/// and the input-binding table for the selected device.
/// </summary>
public sealed class DevicesPageViewModel : ViewModelBase, IDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly IProfileState _profileState;
    private readonly ILogger<DevicesPageViewModel> _logger;
    private DeviceViewModel? _selectedDevice;

    /// <summary>
    /// Initializes a new instance of <see cref="DevicesPageViewModel"/>.
    /// </summary>
    public DevicesPageViewModel(
        IDeviceManager deviceManager,
        IProfileState profileState,
        ILogger<DevicesPageViewModel> logger)
    {
        _deviceManager = deviceManager;
        _profileState = profileState;
        _logger = logger;

        Devices = new ObservableCollection<DeviceViewModel>();
        InputEntries = new ObservableCollection<InputEntryViewModel>();

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;
        _deviceManager.InputReceived += OnInputReceived;
        _profileState.ProfileChanged += OnProfileChanged;

        _ = this.WhenAnyValue(x => x.SelectedDevice)
            .Subscribe(_ => RebuildInputEntries());
    }

    /// <summary>Gets the list of connected device ViewModels.</summary>
    public ObservableCollection<DeviceViewModel> Devices { get; }

    /// <summary>Gets the input-binding rows for the currently selected device.</summary>
    public ObservableCollection<InputEntryViewModel> InputEntries { get; }

    /// <summary>Gets or sets the currently selected device.</summary>
    public DeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    /// <summary>
    /// Repopulates <see cref="Devices"/> from <see cref="IDeviceManager.Devices"/>.
    /// Call after <see cref="IDeviceManager.Initialize"/> returns.
    /// </summary>
    public void RefreshDevices()
    {
        Devices.Clear();
        foreach (var device in _deviceManager.Devices)
            Devices.Add(new DeviceViewModel(device));
        SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
    }

    private void RebuildInputEntries()
    {
        InputEntries.Clear();
        if (SelectedDevice is null) return;

        var device = SelectedDevice.Device;
        var profile = _profileState.CurrentProfile;

        for (int i = 1; i <= device.AxisCount; i++)
            InputEntries.Add(CreateEntry(InputType.JoystickAxis, i, device.Guid, profile));
        for (int i = 1; i <= device.ButtonCount; i++)
            InputEntries.Add(CreateEntry(InputType.JoystickButton, i, device.Guid, profile));
        for (int i = 1; i <= device.HatCount; i++)
            InputEntries.Add(CreateEntry(InputType.JoystickHat, i, device.Guid, profile));
    }

    private static InputEntryViewModel CreateEntry(
        InputType inputType, int index, Guid deviceGuid, JoystickGremlin.Core.Profile.Profile? profile)
    {
        var entry = new InputEntryViewModel(inputType, index);
        if (profile is not null)
        {
            var tags = profile.Modes
                .SelectMany(m => m.Bindings)
                .Where(b => b.DeviceGuid == deviceGuid && b.InputType == inputType && b.Identifier == index)
                .SelectMany(b => b.Actions)
                .Select(a => a.ActionTag)
                .Distinct()
                .ToList();
            if (tags.Count > 0)
                entry.BoundActions = string.Join(", ", tags);
        }
        return entry;
    }

    private void OnDeviceConnected(object? sender, IPhysicalDevice device)
    {
        Dispatcher.UIThread.Post(() => Devices.Add(new DeviceViewModel(device)));
    }

    private void OnDeviceDisconnected(object? sender, IPhysicalDevice device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = Devices.FirstOrDefault(d => d.Device.Guid == device.Guid);
            if (vm is not null) Devices.Remove(vm);
        });
    }

    private void OnInputReceived(object? sender, InputEvent inputEvent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (SelectedDevice?.Device.Guid != inputEvent.DeviceGuid) return;
            var entry = InputEntries.FirstOrDefault(
                e => e.InputType == inputEvent.InputType && e.Index == inputEvent.Identifier);
            if (entry is null) return;

            entry.IsActive = true;
            Observable.Timer(TimeSpan.FromMilliseconds(600))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(_ => entry.IsActive = false);
        });
    }

    private void OnProfileChanged(object? sender, JoystickGremlin.Core.Profile.Profile? profile)
    {
        Dispatcher.UIThread.Post(RebuildInputEntries);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;
        _deviceManager.InputReceived -= OnInputReceived;
        _profileState.ProfileChanged -= OnProfileChanged;
    }
}
