// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace JoystickGremlin.Interop.Startup;

/// <summary>
/// Windows implementation of <see cref="IStartupService"/> that registers the application
/// in the current user's "Run" registry key so it launches automatically at login.
/// </summary>
public sealed class WindowsStartupService : IStartupService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValueName = "JoystickGremlin";

    private readonly ILogger<WindowsStartupService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsStartupService"/>.
    /// </summary>
    public WindowsStartupService(ILogger<WindowsStartupService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
                return key?.GetValue(RegistryValueName) is not null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read startup registry key");
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public void Enable(string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key is null)
            {
                _logger.LogWarning("Could not open registry key {Key} for writing", RegistryKeyPath);
                return;
            }
            key.SetValue(RegistryValueName, $"\"{executablePath}\"");
            _logger.LogInformation("Registered startup entry: {Path}", executablePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register startup entry");
        }
    }

    /// <inheritdoc/>
    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key?.GetValue(RegistryValueName) is null) return;
            key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
            _logger.LogInformation("Removed startup registry entry");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove startup entry");
        }
    }
}
