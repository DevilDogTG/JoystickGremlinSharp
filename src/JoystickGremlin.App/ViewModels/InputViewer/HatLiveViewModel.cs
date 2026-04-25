// SPDX-License-Identifier: GPL-3.0-only

using ReactiveUI;

namespace JoystickGremlin.App.ViewModels.InputViewer;

/// <summary>
/// Represents the live direction of a single hat (POV) switch.
/// </summary>
public sealed class HatLiveViewModel : ReactiveObject
{
    private int _directionDegrees = -1;

    /// <summary>Gets the 1-based hat index.</summary>
    public int HatIndex { get; }

    /// <summary>Gets the display label (e.g. "Hat 1").</summary>
    public string Label { get; }

    /// <summary>
    /// Gets or sets the current hat direction in centidegrees (0–35999), or -1 for center/released.
    /// </summary>
    public int DirectionDegrees
    {
        get => _directionDegrees;
        set
        {
            this.RaiseAndSetIfChanged(ref _directionDegrees, value);
            this.RaisePropertyChanged(nameof(DirectionLabel));
            this.RaisePropertyChanged(nameof(IsNorth));
            this.RaisePropertyChanged(nameof(IsNorthEast));
            this.RaisePropertyChanged(nameof(IsEast));
            this.RaisePropertyChanged(nameof(IsSouthEast));
            this.RaisePropertyChanged(nameof(IsSouth));
            this.RaisePropertyChanged(nameof(IsSouthWest));
            this.RaisePropertyChanged(nameof(IsWest));
            this.RaisePropertyChanged(nameof(IsNorthWest));
            this.RaisePropertyChanged(nameof(IsCenter));
        }
    }

    /// <summary>Gets a human-readable direction label (e.g. "N", "NE", "Center").</summary>
    public string DirectionLabel => DirectionDegrees switch
    {
        -1                        => "Center",
        >= 0     and < 2250       => "N",
        >= 2250  and < 6750       => "NE",
        >= 6750  and < 11250      => "E",
        >= 11250 and < 15750      => "SE",
        >= 15750 and < 20250      => "S",
        >= 20250 and < 24750      => "SW",
        >= 24750 and < 29250      => "W",
        >= 29250 and < 33750      => "NW",
        _                         => "N",  // >= 33750 wraps back to N
    };

    /// <summary>Gets whether the hat is pointing north.</summary>
    public bool IsNorth     => DirectionLabel == "N";
    /// <summary>Gets whether the hat is pointing northeast.</summary>
    public bool IsNorthEast => DirectionLabel == "NE";
    /// <summary>Gets whether the hat is pointing east.</summary>
    public bool IsEast      => DirectionLabel == "E";
    /// <summary>Gets whether the hat is pointing southeast.</summary>
    public bool IsSouthEast => DirectionLabel == "SE";
    /// <summary>Gets whether the hat is pointing south.</summary>
    public bool IsSouth     => DirectionLabel == "S";
    /// <summary>Gets whether the hat is pointing southwest.</summary>
    public bool IsSouthWest => DirectionLabel == "SW";
    /// <summary>Gets whether the hat is pointing west.</summary>
    public bool IsWest      => DirectionLabel == "W";
    /// <summary>Gets whether the hat is pointing northwest.</summary>
    public bool IsNorthWest => DirectionLabel == "NW";
    /// <summary>Gets whether the hat is at center.</summary>
    public bool IsCenter    => DirectionDegrees == -1;

    /// <summary>
    /// Initializes a new instance of <see cref="HatLiveViewModel"/>.
    /// </summary>
    public HatLiveViewModel(int hatIndex)
    {
        HatIndex = hatIndex;
        Label    = $"Hat {hatIndex}";
    }
}
