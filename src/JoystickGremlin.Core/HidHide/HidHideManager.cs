// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Core.HidHide;

/// <summary>
/// Whitelists this application's executable in HidHide so games can find the
/// physical devices that the user has hidden via the native HidHide client.
/// Device hiding is configured by the user in the native HidHide UI; this class
/// only manages the application-path whitelist entry.
/// </summary>
/// <param name="controller">The low-level HidHide driver controller.</param>
/// <param name="logger">Logger for diagnostic output.</param>
public sealed class HidHideManager(
    IHidHideController controller,
    ILogger<HidHideManager> logger) : IHidHideManager
{
    private readonly string _ownExePath =
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    private bool _disposed;

    /// <inheritdoc/>
    public Task InitializeAsync()
    {
        if (!controller.IsInstalled || string.IsNullOrEmpty(_ownExePath))
        {
            return Task.CompletedTask;
        }

        try
        {
            controller.Refresh();

            // Read current whitelist (no admin); write only if absent (write may need admin).
            if (controller.ApplicationPaths.Contains(_ownExePath, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogDebug("HidHide: own executable already whitelisted '{Path}'", _ownExePath);
                return Task.CompletedTask;
            }

            controller.AddApplicationPath(_ownExePath);
            logger.LogInformation("HidHide: whitelisted own executable '{Path}'", _ownExePath);
        }
        catch (HidHideElevationCancelledException)
        {
            logger.LogInformation("HidHide: user declined elevation — app not added to whitelist; device hiding may not work correctly");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HidHide initialization failed");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_ownExePath) && controller.IsInstalled)
        {
            try
            {
                if (controller.ApplicationPaths.Contains(_ownExePath, StringComparer.OrdinalIgnoreCase))
                {
                    controller.RemoveApplicationPath(_ownExePath);
                    logger.LogDebug("HidHide: removed own exe from whitelist on exit");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "HidHide: failed to remove own exe from whitelist on exit");
            }
        }

        _disposed = true;
    }
}
