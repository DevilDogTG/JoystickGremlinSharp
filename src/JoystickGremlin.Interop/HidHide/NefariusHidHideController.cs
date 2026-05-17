// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.HidHide;
using Microsoft.Extensions.Logging;
using Nefarius.Drivers.HidHide;

namespace JoystickGremlin.Interop.HidHide;

/// <summary>
/// <see cref="IHidHideController"/> implementation backed by the <see cref="IHidHideControlService"/>
/// from the Nefarius.Drivers.HidHide NuGet package. Falls back to <see cref="HidHideCliFallback"/>
/// if the IOCTL call throws.
/// </summary>
internal sealed class NefariusHidHideController : IHidHideController
{
    private readonly IHidHideControlService _service;
    private readonly HidHideCliFallback _cli;
    private readonly ILogger<NefariusHidHideController> _logger;

    public NefariusHidHideController(
        IHidHideControlService service,
        HidHideCliFallback cli,
        ILogger<NefariusHidHideController> logger)
    {
        _service = service;
        _cli = cli;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsInstalled => TryGet(() => _service.IsInstalled, fallback: false);

    /// <inheritdoc/>
    public bool IsActive
    {
        get => TryGet(() => _service.IsActive, fallback: false);
        set => TrySet(() => { _service.IsActive = value; }, () => _cli.SetActive(value));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> BlockedInstanceIds =>
        TryGet(() => _service.BlockedInstanceIds, fallback: []);

    /// <inheritdoc/>
    public IReadOnlyList<string> ApplicationPaths =>
        TryGet(() => _service.ApplicationPaths, fallback: []);

    /// <inheritdoc/>
    public void AddBlockedInstance(string instanceId) =>
        TrySet(
            () => _service.AddBlockedInstanceId(instanceId),
            () => _cli.AddBlockedInstance(instanceId));

    /// <inheritdoc/>
    public void RemoveBlockedInstance(string instanceId) =>
        TrySet(
            () => _service.RemoveBlockedInstanceId(instanceId),
            () => _cli.RemoveBlockedInstance(instanceId));

    /// <inheritdoc/>
    public void AddApplicationPath(string fullPath) =>
        TrySet(
            () => _service.AddApplicationPath(fullPath),
            () => _cli.AddApplicationPath(fullPath));

    /// <inheritdoc/>
    public void RemoveApplicationPath(string fullPath) =>
        TrySet(
            () => _service.RemoveApplicationPath(fullPath),
            () => _cli.RemoveApplicationPath(fullPath));

    /// <inheritdoc/>
    public void Refresh()
    {
        // Nefarius.Drivers.HidHide reads state fresh on each property access; nothing to do.
    }

    // ── Helper: IOCTL → CLI fallback ─────────────────────────────────────────

    private T TryGet<T>(Func<T> ioctl, T fallback)
    {
        try
        {
            return ioctl();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HidHide IOCTL read failed");
            return fallback;
        }
    }

    private void TrySet(Action ioctl, Action cliFallback)
    {
        // Always use the CLI tool for write operations — it reliably communicates with
        // the HidHide driver on all installation configurations, including machines where
        // the IOCTL device interface GUID is not accessible or silently no-ops.
        _ = ioctl; // kept for signature parity; not invoked
        try
        {
            cliFallback();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HidHide CLI write failed");
            throw;
        }
    }
}
