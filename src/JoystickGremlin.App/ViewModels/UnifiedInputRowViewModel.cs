// SPDX-License-Identifier: GPL-3.0-only

using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.App.ViewModels.InputViewer;
using JoystickGremlin.Core.Devices;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Represents a single physical input row in the merged controller setup page.
/// Combines compact live state indicators with the current binding summary text.
/// </summary>
public sealed class UnifiedInputRowViewModel : ViewModelBase, IDisposable
{
    private bool _isActive;
    private string _boundActions = "(none)";
    private IDisposable? _activeTimer;

    /// <summary>
    /// Initializes a new instance of <see cref="UnifiedInputRowViewModel"/>.
    /// </summary>
    public UnifiedInputRowViewModel(
        InputType inputType,
        int identifier,
        AxisLiveViewModel? axis = null,
        ButtonLiveViewModel? button = null,
        HatLiveViewModel? hat = null)
    {
        InputType = inputType;
        Identifier = identifier;
        Axis = axis;
        Button = button;
        Hat = hat;
    }

    /// <summary>Gets the input type.</summary>
    public InputType InputType { get; }

    /// <summary>Gets the 1-based input identifier.</summary>
    public int Identifier { get; }

    /// <summary>Gets the live axis model for axis rows.</summary>
    public AxisLiveViewModel? Axis { get; }

    /// <summary>Gets the live button model for button rows.</summary>
    public ButtonLiveViewModel? Button { get; }

    /// <summary>Gets the live hat model for hat rows.</summary>
    public HatLiveViewModel? Hat { get; }

    /// <summary>Gets a human-readable row label such as "Axis 1" or "Button 3".</summary>
    public string Label => InputType switch
    {
        InputType.JoystickAxis => $"Axis {Identifier}",
        InputType.JoystickButton => $"Button {Identifier}",
        InputType.JoystickHat => $"Hat {Identifier}",
        _ => $"{InputType} {Identifier}",
    };

    /// <summary>Gets whether this row represents an axis.</summary>
    public bool IsAxis => Axis is not null;

    /// <summary>Gets whether this row represents a button.</summary>
    public bool IsButton => Button is not null;

    /// <summary>Gets whether this row represents a hat.</summary>
    public bool IsHat => Hat is not null;

    /// <summary>Gets or sets the binding summary text shown for this row.</summary>
    public string BoundActions
    {
        get => _boundActions;
        set => this.RaiseAndSetIfChanged(ref _boundActions, value);
    }

    /// <summary>Gets or sets a value indicating whether this input was recently active.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    /// <summary>
    /// Marks this row as active and automatically clears the flag after 600 ms.
    /// Cancels any pending clear from a previous call, preventing stale timers accumulating.
    /// Must be called on the UI thread.
    /// </summary>
    public void MarkActive()
    {
        IsActive = true;
        _activeTimer?.Dispose();
        _activeTimer = Observable.Timer(TimeSpan.FromMilliseconds(600))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(_ =>
            {
                IsActive = false;
                _activeTimer = null;
            });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _activeTimer?.Dispose();
        _activeTimer = null;
    }
}
