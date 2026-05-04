// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using JoystickGremlin.Core.Devices;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the virtual devices page.
/// </summary>
public sealed class VirtualDevicesPageViewModel : ViewModelBase, IDisposable
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<VirtualDevicesPageViewModel> _logger;
    private readonly ILogger<VirtualDeviceViewModel> _deviceLogger;
    private readonly CompositeDisposable _subscriptions = [];
    private VirtualDeviceViewModel? _selectedDevice;

    /// <summary>
    /// Initializes a new instance of <see cref="VirtualDevicesPageViewModel"/>.
    /// </summary>
    public VirtualDevicesPageViewModel(
        IVirtualDeviceManager virtualDeviceManager,
        ILogger<VirtualDevicesPageViewModel> logger,
        ILogger<VirtualDeviceViewModel> deviceLogger)
    {
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;
        _deviceLogger = deviceLogger;

        Devices = [];

        RefreshCommand = ReactiveCommand.Create(RefreshDevices);
        OpenVJoyControlPanelCommand = ReactiveCommand.Create(OpenVJoyControlPanel);

        _subscriptions.Add(
            Observable.Interval(TimeSpan.FromMilliseconds(200))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(_ => SelectedDevice?.Refresh()));
    }

    /// <summary>Gets the configured virtual devices.</summary>
    public ObservableCollection<VirtualDeviceViewModel> Devices { get; }

    /// <summary>Gets or sets the selected virtual device.</summary>
    public VirtualDeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    /// <summary>Gets whether the configuration tool is available.</summary>
    public bool HasConfigurationTool => !string.IsNullOrWhiteSpace(_virtualDeviceManager.GetConfigurationToolPath());

    /// <summary>Gets the command that refreshes the device list.</summary>
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>Gets the command that opens the vJoy configuration tool.</summary>
    public ReactiveCommand<Unit, Unit> OpenVJoyControlPanelCommand { get; }

    /// <summary>
    /// Refreshes the virtual device list.
    /// </summary>
    public void RefreshDevices()
    {
        var selectedDeviceId = SelectedDevice?.DeviceId;

        Devices.Clear();

        foreach (var deviceId in _virtualDeviceManager.GetAvailableDeviceIds())
        {
            var device = new VirtualDeviceViewModel(deviceId, _virtualDeviceManager, _deviceLogger);
            device.Refresh();
            Devices.Add(device);
        }

        SelectedDevice = selectedDeviceId is null
            ? Devices.FirstOrDefault()
            : Devices.FirstOrDefault(device => device.DeviceId == selectedDeviceId) ?? Devices.FirstOrDefault();

        this.RaisePropertyChanged(nameof(HasConfigurationTool));
    }

    private void OpenVJoyControlPanel()
    {
        var toolPath = _virtualDeviceManager.GetConfigurationToolPath();
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            _logger.LogWarning("vJoy configuration tool could not be located");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = toolPath,
            WorkingDirectory = Path.GetDirectoryName(toolPath),
            UseShellExecute = true,
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _subscriptions.Dispose();
    }
}
