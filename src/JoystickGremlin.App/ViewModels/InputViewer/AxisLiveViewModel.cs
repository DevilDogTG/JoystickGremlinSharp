// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels.InputViewer;

/// <summary>
/// Represents the live state of a single joystick axis.
/// </summary>
public sealed class AxisLiveViewModel : ReactiveObject
{
    private double _value;

    /// <summary>Gets the 1-based axis index.</summary>
    public int AxisIndex { get; }

    /// <summary>Gets the display label (e.g. "Axis 1").</summary>
    public string Label { get; }

    /// <summary>
    /// Gets or sets the current normalized axis value in the range [-1.0, 1.0].
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            this.RaiseAndSetIfChanged(ref _value, value);
            this.RaisePropertyChanged(nameof(DisplayPercent));
            this.RaisePropertyChanged(nameof(NegativePercent));
            this.RaisePropertyChanged(nameof(PositivePercent));
            this.RaisePropertyChanged(nameof(BarLeftWidth));
            this.RaisePropertyChanged(nameof(BarFillWidth));
            this.RaisePropertyChanged(nameof(BarRightWidth));
            this.RaisePropertyChanged(nameof(ValueLabel));
        }
    }

    /// <summary>
    /// Gets the axis value mapped to [0, 100] where 50 = center.
    /// Used by a bidirectional ProgressBar to fill left or right from center.
    /// </summary>
    public double DisplayPercent => (Value + 1.0) * 50.0;

    /// <summary>Gets the left-half fill amount in the range [0, 100] for negative values.</summary>
    public double NegativePercent => Math.Clamp(-Value, 0.0, 1.0) * 100.0;

    /// <summary>Gets the right-half fill amount in the range [0, 100] for positive values.</summary>
    public double PositivePercent => Math.Clamp(Value, 0.0, 1.0) * 100.0;

    /// <summary>
    /// Gets the left empty-space column width for the 3-column centered fill indicator.
    /// Together with <see cref="BarFillWidth"/> and <see cref="BarRightWidth"/> these
    /// place the fill rectangle at exactly the correct position regardless of direction.
    /// </summary>
    public GridLength BarLeftWidth  => new(Value < 0 ? (1.0 + Value) / 2.0 : 0.5, GridUnitType.Star);

    /// <summary>Gets the fill column width for the 3-column centered fill indicator.</summary>
    public GridLength BarFillWidth  => new(Math.Abs(Value) / 2.0, GridUnitType.Star);

    /// <summary>Gets the right empty-space column width for the 3-column centered fill indicator.</summary>
    public GridLength BarRightWidth => new(Value > 0 ? (1.0 - Value) / 2.0 : 0.5, GridUnitType.Star);

    /// <summary>Gets a formatted string representation of the current value (e.g. "-0.42").</summary>
    public string ValueLabel => Value.ToString("F2");

    /// <summary>
    /// Initializes a new instance of <see cref="AxisLiveViewModel"/>.
    /// </summary>
    public AxisLiveViewModel(int axisIndex)
    {
        AxisIndex = axisIndex;
        Label     = $"Axis {axisIndex}";
    }
}
