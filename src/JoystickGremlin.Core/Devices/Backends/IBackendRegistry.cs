// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices.Backends;

/// <summary>
/// Holds the set of registered <see cref="IVirtualDeviceBackend"/> instances and
/// resolves them by id.
/// </summary>
public interface IBackendRegistry
{
    /// <summary>Gets all registered backends.</summary>
    IReadOnlyList<IVirtualDeviceBackend> Backends { get; }

    /// <summary>Identifier of the backend treated as default when a profile does not pin one.</summary>
    string DefaultBackendId { get; }

    /// <summary>Returns the backend with the given id, or <c>null</c> if none registered.</summary>
    IVirtualDeviceBackend? Find(string id);

    /// <summary>Returns the backend with the given id, falling back to the default backend
    /// when the id is null/unknown. Throws if no backends are registered.</summary>
    IVirtualDeviceBackend Resolve(string? id);
}
