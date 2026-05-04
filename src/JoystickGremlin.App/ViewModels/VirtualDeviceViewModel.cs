// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using JoystickGremlin.App.ViewModels.InputViewer;
using JoystickGremlin.Core.Devices;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel representing a single configured vJoy device slot.
/// </summary>
public sealed class VirtualDeviceViewModel : ViewModelBase
{
    private readonly IVirtualDeviceManager _virtualDeviceManager;
    private readonly ILogger<VirtualDeviceViewModel> _logger;
    private VirtualDeviceStatus _status;
    private int _axisCount;
    private int _buttonCount;
    private int _hatCount;

    /// <summary>
    /// Initializes a new instance of <see cref="VirtualDeviceViewModel"/>.
    /// </summary>
    public VirtualDeviceViewModel(
        uint deviceId,
        IVirtualDeviceManager virtualDeviceManager,
        ILogger<VirtualDeviceViewModel> logger)
    {
        DeviceId = deviceId;
        _virtualDeviceManager = virtualDeviceManager;
        _logger = logger;

        Axes = [];
        Buttons = [];
        Hats = [];

        AcquireCommand = ReactiveCommand.Create(AcquireDevice);
        ReleaseCommand = ReactiveCommand.Create(ReleaseDevice);
        ResetCommand = ReactiveCommand.Create(ResetDevice);
    }

    /// <summary>Gets the vJoy device ID.</summary>
    public uint DeviceId { get; }

    /// <summary>Gets the live axis values currently tracked for this device.</summary>
    public ObservableCollection<AxisLiveViewModel> Axes { get; }

    /// <summary>Gets the live button values currently tracked for this device.</summary>
    public ObservableCollection<ButtonLiveViewModel> Buttons { get; }

    /// <summary>Gets the live hat values currently tracked for this device.</summary>
    public ObservableCollection<HatLiveViewModel> Hats { get; }

    /// <summary>Gets the current status of the device.</summary>
    public VirtualDeviceStatus Status
    {
        get => _status;
        private set
        {
            this.RaiseAndSetIfChanged(ref _status, value);
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(IsOwned));
            this.RaisePropertyChanged(nameof(CanAcquire));
            this.RaisePropertyChanged(nameof(CanRelease));
            this.RaisePropertyChanged(nameof(CanReset));
        }
    }

    /// <summary>Gets the configured axis count.</summary>
    public int AxisCount
    {
        get => _axisCount;
        private set => this.RaiseAndSetIfChanged(ref _axisCount, value);
    }

    /// <summary>Gets the configured button count.</summary>
    public int ButtonCount
    {
        get => _buttonCount;
        private set => this.RaiseAndSetIfChanged(ref _buttonCount, value);
    }

    /// <summary>Gets the configured hat count.</summary>
    public int HatCount
    {
        get => _hatCount;
        private set => this.RaiseAndSetIfChanged(ref _hatCount, value);
    }

    /// <summary>Gets the display title.</summary>
    public string Title => $"vJoy {DeviceId}";

    /// <summary>Gets the status as a short label.</summary>
    public string StatusText => Status switch
    {
        VirtualDeviceStatus.Owned => "Owned by this app",
        VirtualDeviceStatus.Free => "Free",
        VirtualDeviceStatus.Busy => "Busy",
        VirtualDeviceStatus.Missing => "Missing",
        _ => "Unknown",
    };

    /// <summary>Gets whether this device is owned by this process.</summary>
    public bool IsOwned => Status == VirtualDeviceStatus.Owned;

    /// <summary>Gets whether the device can be acquired.</summary>
    public bool CanAcquire => Status == VirtualDeviceStatus.Free;

    /// <summary>Gets whether the device can be released.</summary>
    public bool CanRelease => Status == VirtualDeviceStatus.Owned;

    /// <summary>Gets whether the device can be reset.</summary>
    public bool CanReset => Status == VirtualDeviceStatus.Owned;

    /// <summary>Gets whether the device has axes.</summary>
    public bool HasAxes => AxisCount > 0;

    /// <summary>Gets whether the device has buttons.</summary>
    public bool HasButtons => ButtonCount > 0;

    /// <summary>Gets whether the device has hats.</summary>
    public bool HasHats => HatCount > 0;

    /// <summary>Gets the command that acquires the device.</summary>
    public ReactiveCommand<Unit, Unit> AcquireCommand { get; }

    /// <summary>Gets the command that releases the device.</summary>
    public ReactiveCommand<Unit, Unit> ReleaseCommand { get; }

    /// <summary>Gets the command that resets the device outputs.</summary>
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    /// <summary>
    /// Refreshes the device capabilities, status, and tracked live values.
    /// </summary>
    public void Refresh()
    {
        var capabilities = _virtualDeviceManager.GetCapabilities(DeviceId);
        AxisCount = capabilities.AxisCount;
        ButtonCount = capabilities.ButtonCount;
        HatCount = capabilities.HatCount;

        EnsureCollections();

        Status = _virtualDeviceManager.GetStatus(DeviceId);
        RefreshLiveValues();

        this.RaisePropertyChanged(nameof(HasAxes));
        this.RaisePropertyChanged(nameof(HasButtons));
        this.RaisePropertyChanged(nameof(HasHats));
    }

    /// <summary>
    /// Refreshes only the tracked live values.
    /// </summary>
    public void RefreshLiveValues()
    {
        if (Status != VirtualDeviceStatus.Owned)
        {
            ResetDisplayedValues();
            return;
        }

        var device = _virtualDeviceManager.GetDevice(DeviceId);

        for (var i = 1; i <= AxisCount; i++)
            Axes[i - 1].Value = device.GetAxis(i) ?? 0.0;

        for (var i = 1; i <= ButtonCount; i++)
            Buttons[i - 1].IsPressed = device.GetButton(i) ?? false;

        for (var i = 1; i <= HatCount; i++)
            Hats[i - 1].DirectionDegrees = device.GetHat(i) ?? -1;
    }

    private void AcquireDevice()
    {
        try
        {
            _virtualDeviceManager.AcquireDevice(DeviceId);
            Refresh();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire vJoy device {DeviceId}", DeviceId);
        }
    }

    private void ReleaseDevice()
    {
        _virtualDeviceManager.ReleaseDevice(DeviceId);
        Refresh();
    }

    private void ResetDevice()
    {
        if (Status != VirtualDeviceStatus.Owned)
            return;

        _virtualDeviceManager.GetDevice(DeviceId).Reset();
        RefreshLiveValues();
    }

    private void EnsureCollections()
    {
        if (Axes.Count != AxisCount)
        {
            Axes.Clear();
            for (var i = 1; i <= AxisCount; i++)
                Axes.Add(new AxisLiveViewModel(i));
        }

        if (Buttons.Count != ButtonCount)
        {
            Buttons.Clear();
            for (var i = 1; i <= ButtonCount; i++)
                Buttons.Add(new ButtonLiveViewModel(i));
        }

        if (Hats.Count != HatCount)
        {
            Hats.Clear();
            for (var i = 1; i <= HatCount; i++)
                Hats.Add(new HatLiveViewModel(i));
        }
    }

    private void ResetDisplayedValues()
    {
        foreach (var axis in Axes)
            axis.Value = 0.0;

        foreach (var button in Buttons)
            button.IsPressed = false;

        foreach (var hat in Hats)
            hat.DirectionDegrees = -1;
    }
}
