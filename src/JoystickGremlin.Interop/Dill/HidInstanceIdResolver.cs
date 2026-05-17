// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Win32;

namespace JoystickGremlin.Interop.Dill;

/// <summary>
/// Resolves Windows Device Instance IDs for HID devices by scanning the device registry
/// under <c>HKLM\SYSTEM\CurrentControlSet\Enum\HID</c>, matching by VendorId and ProductId.
/// </summary>
internal static class HidInstanceIdResolver
{
    private const string HidEnumPath = @"SYSTEM\CurrentControlSet\Enum\HID";

    /// <summary>
    /// Builds a map of (VendorId, ProductId) → first matching Device Instance ID.
    /// When multiple physical devices share the same VID/PID the first present entry wins.
    /// </summary>
    internal static IReadOnlyDictionary<(uint vid, uint pid), string> BuildInstanceIdMap()
    {
        var result = new Dictionary<(uint, uint), string>();

        try
        {
            using var hidKey = Registry.LocalMachine.OpenSubKey(HidEnumPath, writable: false);
            if (hidKey is null)
                return result;

            foreach (var hwKeyName in hidKey.GetSubKeyNames())
            {
                // Key name format: "VID_054C&PID_05C4" or "VID_054C&PID_05C4&Col01" etc.
                if (!TryParseVidPid(hwKeyName, out uint vid, out uint pid))
                    continue;

                using var hwKey = hidKey.OpenSubKey(hwKeyName, writable: false);
                if (hwKey is null)
                    continue;

                foreach (var instanceKeyName in hwKey.GetSubKeyNames())
                {
                    // Full instance ID: "HID\VID_054C&PID_05C4\6&1A2B3C4D&0&0000"
                    var instanceId = $@"HID\{hwKeyName}\{instanceKeyName}";
                    result.TryAdd((vid, pid), instanceId);
                }
            }
        }
        catch
        {
            // Registry access failure — return empty map, caller handles null InstanceId
        }

        return result;
    }

    private static bool TryParseVidPid(string keyName, out uint vid, out uint pid)
    {
        vid = 0;
        pid = 0;

        var upper = keyName.ToUpperInvariant();
        var vidIdx = upper.IndexOf("VID_", StringComparison.Ordinal);
        var pidIdx = upper.IndexOf("PID_", StringComparison.Ordinal);

        if (vidIdx < 0 || pidIdx < 0)
            return false;

        try
        {
            vid = Convert.ToUInt32(upper.Substring(vidIdx + 4, 4), 16);
            pid = Convert.ToUInt32(upper.Substring(pidIdx + 4, 4), 16);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

