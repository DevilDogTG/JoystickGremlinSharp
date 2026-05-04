// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices;

/// <summary>
/// Describes the configured input capabilities of a virtual device.
/// </summary>
/// <param name="AxisCount">The number of configured axes.</param>
/// <param name="ButtonCount">The number of configured buttons.</param>
/// <param name="HatCount">The number of configured hats/POVs.</param>
public sealed record VirtualDeviceCapabilities(int AxisCount, int ButtonCount, int HatCount);
