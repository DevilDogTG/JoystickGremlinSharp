// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels.InputViewer;

/// <summary>
/// Holds the complete live input state for a single physical device.
/// Receives raw <see cref="InputEvent"/> values and dispatches updates to the UI thread.
/// </summary>
public sealed class DeviceLiveInputViewModel : ReactiveObject, IDisposable
{
    private readonly IPhysicalDevice _device;

    // Throttle per-input UI updates to ~30 Hz to avoid flooding the UI thread
    // at high polling rates (e.g. 1000 Hz steering wheels).
    private readonly ConcurrentDictionary<int, long> _lastAxisUpdateMs   = new();
    private readonly ConcurrentDictionary<int, long> _lastButtonUpdateMs = new();
    private readonly ConcurrentDictionary<int, long> _lastHatUpdateMs    = new();
    private const long UiUpdateIntervalMs = 33; // ~30 Hz

    /// <summary>Gets the device display name.</summary>
    public string Name => _device.Name;

    /// <summary>Gets the device GUID.</summary>
    public Guid Guid => _device.Guid;

    /// <summary>Gets the live state of all axes for this device.</summary>
    public ObservableCollection<AxisLiveViewModel> Axes { get; } = [];

    /// <summary>Gets the live state of all buttons for this device.</summary>
    public ObservableCollection<ButtonLiveViewModel> Buttons { get; } = [];

    /// <summary>Gets the live state of all hat switches for this device.</summary>
    public ObservableCollection<HatLiveViewModel> Hats { get; } = [];

    /// <summary>Gets a value indicating whether this device has any axes.</summary>
    public bool HasAxes => Axes.Count > 0;

    /// <summary>Gets a value indicating whether this device has any buttons.</summary>
    public bool HasButtons => Buttons.Count > 0;

    /// <summary>Gets a value indicating whether this device has any hats.</summary>
    public bool HasHats => Hats.Count > 0;

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceLiveInputViewModel"/>.
    /// </summary>
    public DeviceLiveInputViewModel(IPhysicalDevice device)
    {
        _device = device;

        for (var i = 1; i <= device.AxisCount;   i++) Axes.Add(new AxisLiveViewModel(i));
        for (var i = 1; i <= device.ButtonCount; i++) Buttons.Add(new ButtonLiveViewModel(i));
        for (var i = 1; i <= device.HatCount;    i++) Hats.Add(new HatLiveViewModel(i));
    }

    /// <summary>
    /// Applies a raw input event, dispatching any property updates to the UI thread.
    /// Updates are throttled to ~30 Hz per input to avoid overwhelming the UI thread
    /// at high polling rates. Safe to call from any thread.
    /// </summary>
    public void ApplyEvent(InputEvent inputEvent)
    {
        var now = Environment.TickCount64;

        switch (inputEvent.InputType)
        {
            case InputType.JoystickAxis:
            {
                var vm = Axes.FirstOrDefault(a => a.AxisIndex == inputEvent.Identifier);
                if (vm is null) return;
                var last = _lastAxisUpdateMs.GetOrAdd(inputEvent.Identifier, 0L);
                if (now - last < UiUpdateIntervalMs) return;
                _lastAxisUpdateMs[inputEvent.Identifier] = now;
                var value = inputEvent.Value;
                Dispatcher.UIThread.Post(() => vm.Value = value);
                break;
            }

            case InputType.JoystickButton:
            {
                var vm = Buttons.FirstOrDefault(b => b.ButtonIndex == inputEvent.Identifier);
                if (vm is null) return;
                var last = _lastButtonUpdateMs.GetOrAdd(inputEvent.Identifier, 0L);
                if (now - last < UiUpdateIntervalMs) return;
                _lastButtonUpdateMs[inputEvent.Identifier] = now;
                var isPressed = inputEvent.Value >= 0.5;
                Dispatcher.UIThread.Post(() => vm.IsPressed = isPressed);
                break;
            }

            case InputType.JoystickHat:
            {
                var vm = Hats.FirstOrDefault(h => h.HatIndex == inputEvent.Identifier);
                if (vm is null) return;
                var last = _lastHatUpdateMs.GetOrAdd(inputEvent.Identifier, 0L);
                if (now - last < UiUpdateIntervalMs) return;
                _lastHatUpdateMs[inputEvent.Identifier] = now;
                var degrees = (int)inputEvent.Value;
                Dispatcher.UIThread.Post(() => vm.DirectionDegrees = degrees);
                break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() { /* No unmanaged resources; event subscription managed by parent. */ }
}
