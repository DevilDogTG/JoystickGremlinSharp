// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// Application-level settings persisted to disk.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Gets or sets the path to the last opened profile file.</summary>
    public string? LastProfilePath { get; set; }

    /// <summary>Gets or sets the vJoy device ID used for output. Defaults to 1.</summary>
    public uint VJoyDeviceId { get; set; } = 1;

    /// <summary>Gets or sets the name of the default mode activated on startup.</summary>
    public string? DefaultModeName { get; set; }

    /// <summary>Gets or sets whether the application should start minimized to the system tray.</summary>
    public bool StartMinimized { get; set; }

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
}
