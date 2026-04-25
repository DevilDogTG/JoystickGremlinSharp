// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel wrapping a single <see cref="IPhysicalDevice"/> for display in the Devices page.
/// </summary>
public sealed class DeviceViewModel : ViewModelBase
{
    private string _lastInputLabel = string.Empty;

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceViewModel"/>.
    /// </summary>
    /// <param name="device">The underlying physical device.</param>
    public DeviceViewModel(IPhysicalDevice device)
    {
        Device = device;
    }

    /// <summary>Gets the underlying physical device.</summary>
    public IPhysicalDevice Device { get; }

    /// <summary>Gets the device name.</summary>
    public string Name => Device.Name;

    /// <summary>Gets a short summary of the device's input counts.</summary>
    public string InputSummary =>
        $"{Device.AxisCount} axes · {Device.ButtonCount} buttons · {Device.HatCount} hats";

    /// <summary>Gets or sets a label describing the most recent input received from this device.</summary>
    public string LastInputLabel
    {
        get => _lastInputLabel;
        set => this.RaiseAndSetIfChanged(ref _lastInputLabel, value);
    }
}
