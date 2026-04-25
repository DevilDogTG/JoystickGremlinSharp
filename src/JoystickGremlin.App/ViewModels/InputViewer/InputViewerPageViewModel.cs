// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using Avalonia.Threading;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels.InputViewer;

/// <summary>
/// ViewModel for the Input Viewer page.
/// Displays the live state of all connected physical device inputs (axes, buttons, hats)
/// without requiring the event pipeline to be running.
/// </summary>
public sealed class InputViewerPageViewModel : ViewModelBase, IDisposable
{
    private readonly IDeviceManager _deviceManager;
    private DeviceLiveInputViewModel? _selectedDevice;

    /// <summary>Gets the collection of live input ViewModels — one per connected device.</summary>
    public ObservableCollection<DeviceLiveInputViewModel> Devices { get; } = [];

    /// <summary>Gets or sets the currently selected device in the left panel.</summary>
    public DeviceLiveInputViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InputViewerPageViewModel"/>.
    /// </summary>
    public InputViewerPageViewModel(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;

        _deviceManager.InputReceived       += OnInputReceived;
        _deviceManager.DeviceConnected     += OnDeviceConnected;
        _deviceManager.DeviceDisconnected  += OnDeviceDisconnected;
    }

    /// <summary>
    /// Rebuilds the device list from the currently connected devices.
    /// Call after <see cref="IDeviceManager.Initialize"/> completes.
    /// </summary>
    public void RefreshDevices()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Devices.Clear();
            foreach (var d in _deviceManager.Devices)
                Devices.Add(new DeviceLiveInputViewModel(d));

            SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
        });
    }

    private void OnInputReceived(object? sender, InputEvent inputEvent)
    {
        // Find the device ViewModel matching this event's GUID and route the update.
        var vm = Devices.FirstOrDefault(d => d.Guid == inputEvent.DeviceGuid);
        vm?.ApplyEvent(inputEvent);
    }

    private void OnDeviceConnected(object? sender, IPhysicalDevice device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Devices.Any(d => d.Guid == device.Guid)) return;
            var vm = new DeviceLiveInputViewModel(device);
            Devices.Add(vm);
            SelectedDevice ??= vm;
        });
    }

    private void OnDeviceDisconnected(object? sender, IPhysicalDevice device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = Devices.FirstOrDefault(d => d.Guid == device.Guid);
            if (vm is null) return;
            var wasSelected = SelectedDevice == vm;
            Devices.Remove(vm);
            vm.Dispose();
            if (wasSelected)
                SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _deviceManager.InputReceived      -= OnInputReceived;
        _deviceManager.DeviceConnected    -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

        foreach (var d in Devices)
            d.Dispose();
    }
}
