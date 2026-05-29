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
public sealed class HidHideManager : IHidHideManager
{
    private readonly IHidHideController _controller;
    private readonly ILogger<HidHideManager> _logger;
    private readonly string _ownExePath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="HidHideManager"/> and captures the running process's
    /// executable path so it can be added to and removed from the HidHide application
    /// bypass-list during the application lifecycle.
    /// </summary>
    /// <param name="controller">The low-level HidHide driver controller.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public HidHideManager(
        IHidHideController controller,
        ILogger<HidHideManager> logger)
    {
        _controller = controller;
        _logger = logger;
        _ownExePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_controller.IsInstalled || string.IsNullOrEmpty(_ownExePath))
        {
            return Task.CompletedTask;
        }

        try
        {
            _controller.Refresh();

            // Read current whitelist (no admin); write only if absent (write may need admin).
            if (_controller.ApplicationPaths.Contains(_ownExePath, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogDebug("HidHide: own executable already whitelisted '{Path}'", _ownExePath);
                return Task.CompletedTask;
            }

            _controller.AddApplicationPath(_ownExePath);
            _logger.LogInformation("HidHide: whitelisted own executable '{Path}'", _ownExePath);
        }
        catch (HidHideElevationCancelledException)
        {
            _logger.LogInformation("HidHide: user declined elevation — app not added to whitelist; device hiding may not work correctly");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HidHide initialization failed");
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

        if (!string.IsNullOrEmpty(_ownExePath) && _controller.IsInstalled)
        {
            try
            {
                if (_controller.ApplicationPaths.Contains(_ownExePath, StringComparer.OrdinalIgnoreCase))
                {
                    _controller.RemoveApplicationPath(_ownExePath);
                    _logger.LogDebug("HidHide: removed own exe from whitelist on exit");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HidHide: failed to remove own exe from whitelist on exit");
            }
        }

        _disposed = true;
    }
}
