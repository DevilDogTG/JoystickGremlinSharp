// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// Application-level settings persisted to disk.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Gets or sets the path to the most recently selected profile in the quick-switch selector.
    /// </summary>
    public string? ActiveProfilePath { get; set; }

    /// <summary>Gets or sets the vJoy device ID used for output. Defaults to 1.</summary>
    public uint VJoyDeviceId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the path to the profiles folder.
    /// When empty or null, defaults to <c>%AppData%\JoystickGremlinSharp\profiles</c>.
    /// </summary>
    public string? ProfilesFolderPath { get; set; }

    /// <summary>Gets or sets whether the application should start minimized to the system tray.</summary>
    public bool StartMinimized { get; set; }

    /// <summary>Gets or sets whether the application should be registered to start with Windows.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Gets or sets whether clicking the window close button minimizes to the system tray instead of exiting.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the auto-load feature is globally enabled.
    /// When <c>false</c>, <see cref="ProcessMappings"/> entries are ignored.
    /// </summary>
    public bool EnableAutoLoading { get; set; } = false;

    /// <summary>
    /// Gets or sets the ordered list of process-to-profile mappings used for auto-loading.
    /// Entries are evaluated in list order; the first match wins.
    /// </summary>
    public List<ProcessProfileMapping> ProcessMappings { get; set; } = [];

    /// <summary>Gets or sets whether the FFB bridge is enabled.</summary>
    public bool EnableFfbBridge { get; set; } = false;

    /// <summary>Gets or sets the vJoy device ID to monitor for FFB data. Defaults to 1.</summary>
    public uint FfbVJoyDeviceId { get; set; } = 1;

    /// <summary>Gets or sets the instance GUID of the physical wheel to send FFB to. Null = auto-discover first MOZA device.</summary>
    public string? FfbWheelInstanceGuid { get; set; }

    /// <summary>Gets or sets the FFB gain multiplier as a percentage (0–100). Defaults to 100.</summary>
    public int FfbGainPercent { get; set; } = 100;
}
