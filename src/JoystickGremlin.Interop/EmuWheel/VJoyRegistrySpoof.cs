// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.EmuWheel;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace JoystickGremlin.Interop.EmuWheel;

/// <summary>
/// Manages the vJoy registry entries required for VID/PID identity spoofing and
/// the sentinel file used for crash-safe auto-recovery.
/// </summary>
/// <remarks>
/// <para>
/// The spoof writes custom <c>VendorID</c> and <c>ProductID</c> values to the vJoy driver
/// service-parameters registry path. These values are read by the vJoy driver when the device
/// is initialised. After writing, the vJoy device slot must be reset (released + reacquired)
/// so the driver re-enumerates with the new identity.
/// </para>
/// <para>
/// Registry write requires elevated (administrator) permissions.
/// If the write fails due to access denial, the failure is logged but does not throw —
/// the pipeline continues with the standard vJoy identity and a diagnostic message is emitted.
/// </para>
/// <para>
/// <b>Sentinel file</b> (<c>%AppData%\JoystickGremlinSharp\emuwheel.spoof-active</c>):
/// Written when the spoof is applied, deleted on restore. If the app exits without restoring,
/// the sentinel file's presence signals the next startup to auto-restore before any UI loads.
/// </para>
/// </remarks>
internal sealed class VJoyRegistrySpoof
{
    /// <summary>
    /// vJoy driver service-parameters registry base path.
    /// Per-device subkeys: <c>Device01</c>, <c>Device02</c>, …
    /// </summary>
    internal const string VJoyParamsKeyBase = @"SYSTEM\CurrentControlSet\Services\vjoy\Parameters";

    /// <summary>Sentinel file name inside %AppData%\JoystickGremlinSharp\.</summary>
    private const string SentinelFileName = "emuwheel.spoof-active";

    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JoystickGremlinSharp");

    internal static string SentinelFilePath { get; } = Path.Combine(AppDataFolder, SentinelFileName);

    private readonly ILogger _logger;

    /// <summary>The device slot the current spoof was applied to (null = none active).</summary>
    private uint? _activeVJoyId;

    /// <summary>Gets whether a spoof is currently recorded as active.</summary>
    internal bool IsActive => _activeVJoyId.HasValue;

    /// <summary>
    /// Gets whether the registry was actually written in this process session.
    /// <c>true</c> indicates the VID/PID values were changed and a driver reload (reboot) is needed.
    /// <c>false</c> means the registry already contained the correct values on apply.
    /// </summary>
    internal bool RegistryChanged { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="VJoyRegistrySpoof"/>.
    /// </summary>
    internal VJoyRegistrySpoof(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies the VID/PID spoof for the specified wheel model to the given vJoy device slot.
    /// Reads the current registry values first; only writes when they differ from the desired
    /// values. When the registry already contains the correct VID/PID (e.g. after a reboot with
    /// the spoof already persisted) this is a true no-op and <see cref="RegistryChanged"/> is
    /// not set.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the spoof is active (either newly written or already correct);
    /// <c>false</c> when the registry write was denied or the key was not found.
    /// </returns>
    internal Task<bool> ApplyAsync(WheelModel model, uint vjoyId, CancellationToken cancellationToken = default)
    {
        var info = WheelModelRegistry.Get(model);
        var subKeyName = $@"{VJoyParamsKeyBase}\Device{vjoyId:D2}";

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKeyName, writable: true);
            if (key is null)
            {
                _logger.LogWarning(
                    "EmuWheel spoof: vJoy parameters key not found at HKLM\\{Key}. " +
                    "Ensure vJoy is installed and the device slot {VJoyId} exists.",
                    subKeyName, vjoyId);
                return Task.FromResult(false);
            }

            // Read current registry values to decide whether a write is actually needed.
            var currentVendorId  = key.GetValue("VendorID")  is int v ? v : (int?)null;
            var currentProductId = key.GetValue("ProductID") is int p ? p : (int?)null;

            _activeVJoyId = vjoyId;

            if (currentVendorId == (int)info.VendorId && currentProductId == (int)info.ProductId)
            {
                // Registry already correct — driver will use the right identity on next boot.
                // No write needed, no reboot flag to set.
                _logger.LogInformation(
                    "EmuWheel spoof already correct in registry: slot={VJoyId}, model={Model}, " +
                    "VID=0x{VID:X4}, PID=0x{PID:X4} — no write performed",
                    vjoyId, model, info.VendorId, info.ProductId);
                return Task.FromResult(true);
            }

            key.SetValue("VendorID",  (int)info.VendorId,  RegistryValueKind.DWord);
            key.SetValue("ProductID", (int)info.ProductId, RegistryValueKind.DWord);
            RegistryChanged = true;

            _logger.LogInformation(
                "EmuWheel spoof applied: slot={VJoyId}, model={Model}, VID=0x{VID:X4}, PID=0x{PID:X4}",
                vjoyId, model, info.VendorId, info.ProductId);

            WriteSentinelFile(model, vjoyId);
            return Task.FromResult(true);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "EmuWheel spoof requires administrator privileges to modify " +
                "HKLM\\{Key}. Run the application as Administrator to enable wheel detection. " +
                "The pipeline will continue with the standard vJoy identity.",
                subKeyName);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmuWheel spoof failed unexpectedly for slot={VJoyId}", vjoyId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Removes the custom VID/PID values from the registry for the currently spoofed slot,
    /// reverting the vJoy device to its default identity on the next driver load (reboot).
    /// Deletes the sentinel file. Safe to call when no spoof is active.
    /// </summary>
    internal Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!_activeVJoyId.HasValue)
        {
            DeleteSentinelFile();
            return Task.CompletedTask;
        }

        var vjoyId = _activeVJoyId.Value;
        var subKeyName = $@"{VJoyParamsKeyBase}\Device{vjoyId:D2}";

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKeyName, writable: true);
            if (key is not null)
            {
                TryDeleteValue(key, "VendorID");
                TryDeleteValue(key, "ProductID");
                _logger.LogInformation(
                    "EmuWheel spoof restored (registry values cleared) for slot={VJoyId}", vjoyId);
            }
            else
            {
                _logger.LogWarning(
                    "EmuWheel restore: key HKLM\\{Key} not found — skipping registry restore", subKeyName);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "EmuWheel restore requires administrator privileges to modify HKLM\\{Key}. " +
                "Run as Administrator or manually reset vJoy via vJoyConf.exe.",
                subKeyName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmuWheel restore failed for slot={VJoyId}", vjoyId);
        }
        finally
        {
            _activeVJoyId = null;
            DeleteSentinelFile();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the sentinel file on a clean exit without restoring the registry.
    /// Call from <see cref="EmuWheelDeviceManager"/> Dispose when EmuWheel is still enabled
    /// so that the VID/PID values persist in the registry across the reboot but the next
    /// startup does not trigger a spurious crash-recovery restore.
    /// </summary>
    internal void ClearSentinelOnExit()
    {
        _activeVJoyId = null;
        DeleteSentinelFile();
    }

    /// <summary>
    /// Checks whether a sentinel file exists from a previous (possibly crashed) session.
    /// If it does, attempts to restore the vJoy registry immediately.
    /// Call this once on application startup before showing the UI.
    /// </summary>
    internal Task RecoverIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SentinelFilePath))
            return Task.CompletedTask;

        _logger.LogWarning(
            "EmuWheel sentinel file found at {Path}. A previous session may have exited without " +
            "restoring the vJoy identity. Attempting auto-restore.",
            SentinelFilePath);

        // Read the sentinel to find which slot needs restoring.
        try
        {
            var lines = File.ReadAllLines(SentinelFilePath);
            if (lines.Length >= 2 && uint.TryParse(lines[1], out var vjoyId))
            {
                _activeVJoyId = vjoyId;
                // No saved VID/PID in sentinel — just delete the spoof values.
            }
        }
        catch (Exception) { /* best effort — sentinel parse failure is non-fatal */ }

        return RestoreAsync(cancellationToken);
    }

    private void WriteSentinelFile(WheelModel model, uint vjoyId)
    {
        try
        {
            Directory.CreateDirectory(AppDataFolder);
            File.WriteAllLines(SentinelFilePath, [model.ToString(), vjoyId.ToString()]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write EmuWheel sentinel file");
        }
    }

    private void DeleteSentinelFile()
    {
        try
        {
            if (File.Exists(SentinelFilePath))
                File.Delete(SentinelFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete EmuWheel sentinel file");
        }
    }

    private static void TryDeleteValue(RegistryKey key, string valueName)
    {
        try { key.DeleteValue(valueName, throwOnMissingValue: false); }
        catch (Exception) { /* best effort */ }
    }
}
