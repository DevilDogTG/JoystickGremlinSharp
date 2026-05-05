// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.EmuWheel;

/// <summary>
/// Forces the vJoy virtual bus to re-enumerate its child HID devices via the Windows
/// Configuration Manager API. This is required after writing VID/PID values to the
/// vJoy service-parameters registry: the registry write alone does not cause Windows to
/// update the hardware ID already presented to the device tree — an explicit re-enumeration
/// signal is needed to make the vJoy bus recreate its child devices with the new identity.
/// </summary>
/// <remarks>
/// Requires administrator privileges. If the process is not elevated, the re-enumeration
/// call is skipped and the caller should fall back to showing a reboot-required message.
/// </remarks>
internal sealed class VJoyDeviceReenumerator
{
    /// <summary>Hardware ID prefix used to locate the vJoy bus device in the PnP tree.</summary>
    private const string VJoyBusHardwareIdPrefix = "root\\vjoy";

    private readonly ILogger _logger;

    internal VJoyDeviceReenumerator(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Locates the vJoy virtual bus device in the Windows PnP device tree and calls
    /// <c>CM_Reenumerate_DevNode</c> to force it to recreate all child HID devices.
    /// After re-enumeration the vJoy HID children will be created with VID/PID values
    /// freshly read from the service-parameters registry.
    /// </summary>
    /// <returns>
    /// <c>true</c> when re-enumeration was successfully triggered;
    /// <c>false</c> when the vJoy bus device could not be found or the call was denied.
    /// </returns>
    internal async Task<bool> ReEnumerateVJoyBusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var devNode = FindVJoyBusDevNode();
            if (devNode == 0)
            {
                _logger.LogWarning(
                    "EmuWheel re-enumeration: vJoy bus device not found in PnP tree. " +
                    "Ensure vJoy is installed. A reboot is required for the identity change to take effect.");
                return false;
            }

            // CM_REENUMERATE_SYNCHRONOUS (0x4): wait for re-enumeration to complete.
            // CM_REENUMERATE_RETRY_INSTALLATION (0x2): allow PnP to retry device setup.
            const uint flags = 0x4 | 0x2;
            var cr = NativeMethods.CM_Reenumerate_DevNode(devNode, flags);
            if (cr != 0 /* CR_SUCCESS */)
            {
                _logger.LogWarning(
                    "EmuWheel re-enumeration: CM_Reenumerate_DevNode returned CR=0x{CR:X} for " +
                    "vJoy bus. A reboot may be required for the identity change to take effect.", cr);
                return false;
            }

            _logger.LogInformation(
                "EmuWheel re-enumeration: vJoy bus re-enumerated successfully. " +
                "Child HID devices will reappear with updated VID/PID.");

            // Give PnP a moment to finish settling the device tree before the caller
            // proceeds to acquire the vJoy device slot.
            await Task.Delay(600, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "EmuWheel re-enumeration requires administrator privileges. " +
                "A reboot is required for the identity change to take effect.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmuWheel re-enumeration failed unexpectedly");
            return false;
        }
    }

    /// <summary>
    /// Walks the PnP device tree to find the vJoy bus device node by matching its
    /// hardware ID against the known <c>root\vjoy</c> prefix (case-insensitive).
    /// </summary>
    /// <returns>The device node handle, or 0 when not found.</returns>
    private uint FindVJoyBusDevNode()
    {
        // Most reliable: the Windows SCM maintains a service→device instance ID map under
        // HKLM\SYSTEM\CurrentControlSet\Services\<service>\Enum. Each value 0, 1, … holds
        // the instance ID of a device driven by that service.  Reading this avoids any
        // assumption about the vJoy bus instance ID format.
        var instanceId = ReadVJoyServiceEnumInstanceId();
        if (instanceId is not null)
        {
            var cr = NativeMethods.CM_Locate_DevNodeW(out var devNode, instanceId, 0);
            if (cr == 0 && devNode != 0)
            {
                _logger.LogDebug(
                    "EmuWheel re-enumeration: found vJoy bus via service enum at {InstanceId}",
                    instanceId);
                return devNode;
            }

            _logger.LogDebug(
                "EmuWheel re-enumeration: service enum instance {InstanceId} listed but " +
                "CM_Locate_DevNodeW returned CR=0x{CR:X} — falling back to known IDs",
                instanceId, cr);
        }

        // Fallback: try well-known instance IDs used by common vJoy builds.
        var knownIds = new[] { "ROOT\\VJOYBUS\\0000", "ROOT\\VJOY\\0000", "ROOT\\VJOY\\0001" };
        foreach (var id in knownIds)
        {
            var cr = NativeMethods.CM_Locate_DevNodeW(out var devNode, id, 0);
            if (cr == 0 && devNode != 0)
            {
                _logger.LogDebug(
                    "EmuWheel re-enumeration: found vJoy bus at known instance {InstanceId}", id);
                return devNode;
            }
        }

        // Last resort: walk the PnP device tree matching by hardware ID.
        return FindDevNodeByHardwareId(VJoyBusHardwareIdPrefix);
    }

    /// <summary>
    /// Reads the first device instance ID registered under the vJoy service enum key:
    /// <c>HKLM\SYSTEM\CurrentControlSet\Services\vjoy\Enum\0</c>.
    /// Returns <c>null</c> when the key or value is absent.
    /// </summary>
    private string? ReadVJoyServiceEnumInstanceId()
    {
        const string keyPath = @"SYSTEM\CurrentControlSet\Services\vjoy\Enum";
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            var value = key?.GetValue("0") as string;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "EmuWheel re-enumeration: could not read vJoy service enum registry key");
        }
        return null;
    }

    /// <summary>
    /// Walks the root of the PnP device tree and returns the devnode whose hardware ID
    /// buffer contains a string starting with <paramref name="hwIdPrefix"/> (case-insensitive).
    /// </summary>
    private uint FindDevNodeByHardwareId(string hwIdPrefix)
    {
        // Get the root device node.
        var cr = NativeMethods.CM_Locate_DevNodeW(out var rootNode, null!, 0x4 /* CM_LOCATE_DEVNODE_PHANTOM */);
        if (cr != 0 || rootNode == 0)
        {
            _logger.LogDebug("EmuWheel re-enumeration: CM_Locate_DevNode(root) failed with CR=0x{CR:X}", cr);
            return 0;
        }

        return WalkDevNodeChildren(rootNode, hwIdPrefix);
    }

    private uint WalkDevNodeChildren(uint parent, string hwIdPrefix)
    {
        var cr = NativeMethods.CM_Get_Child(out var child, parent, 0);
        if (cr != 0 || child == 0)
            return 0;

        if (DevNodeHasHardwareId(child, hwIdPrefix))
            return child;

        // Check siblings of the first child.
        var sibling = child;
        while (true)
        {
            cr = NativeMethods.CM_Get_Sibling(out var next, sibling, 0);
            if (cr != 0 || next == 0)
                break;

            if (DevNodeHasHardwareId(next, hwIdPrefix))
                return next;

            sibling = next;
        }

        return 0;
    }

    private static bool DevNodeHasHardwareId(uint devNode, string hwIdPrefix)
    {
        // CM_DRP_HARDWAREID = 2 (hardware ID registry property).
        uint bufferSize = 512;
        uint dataType   = 0;
        var  buffer     = new byte[bufferSize];

        var cr = NativeMethods.CM_Get_DevNode_Registry_PropertyW(
            devNode, 2 /* CM_DRP_HARDWAREID */, ref dataType,
            buffer, ref bufferSize, 0);

        if (cr != 0)
            return false;

        // Hardware ID is a REG_MULTI_SZ — multiple null-separated strings, double-null terminated.
        // Decode all strings and check each one.
        var chars = System.Text.Encoding.Unicode.GetString(buffer, 0, (int)bufferSize);
        var ids = chars.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        return ids.Any(id => id.StartsWith(hwIdPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static class NativeMethods
    {
        private const string CfgMgrDll = "CfgMgr32.dll";

        [DllImport(CfgMgrDll, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Locate_DevNodeW(
            out uint pdnDevInst,
            string?  pDeviceID,
            uint     ulFlags);

        [DllImport(CfgMgrDll)]
        internal static extern uint CM_Reenumerate_DevNode(
            uint devInst,
            uint ulFlags);

        [DllImport(CfgMgrDll)]
        internal static extern uint CM_Get_Child(
            out uint pdnDevInst,
            uint     dnDevInst,
            uint     ulFlags);

        [DllImport(CfgMgrDll)]
        internal static extern uint CM_Get_Sibling(
            out uint pdnDevInst,
            uint     dnDevInst,
            uint     ulFlags);

        [DllImport(CfgMgrDll, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Get_DevNode_Registry_PropertyW(
            uint     dnDevInst,
            uint     ulProperty,
            ref uint pulRegDataType,
            byte[]   Buffer,
            ref uint pulLength,
            uint     ulFlags);
    }
}
