// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.ForceFeedback;

/// <summary>
/// Describes a physical force feedback wheel device discovered via DirectInput enumeration.
/// </summary>
/// <param name="InstanceGuid">The DirectInput instance GUID uniquely identifying this device instance.</param>
/// <param name="ProductGuid">The DirectInput product GUID encoding the USB VID and PID.</param>
/// <param name="InstanceName">The human-readable instance name of the device.</param>
/// <param name="ProductName">The human-readable product name of the device.</param>
/// <param name="VendorId">The USB vendor ID.</param>
/// <param name="ProductId">The USB product ID.</param>
public sealed record FfbWheelInfo(
    Guid InstanceGuid,
    Guid ProductGuid,
    string InstanceName,
    string ProductName,
    ushort VendorId,
    ushort ProductId);
