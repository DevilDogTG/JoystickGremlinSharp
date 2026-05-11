// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Devices.Backends;

/// <summary>
/// Default <see cref="IBackendRegistry"/> implementation. The first backend
/// registered (typically vJoy) is used as the default fallback.
/// </summary>
public sealed class BackendRegistry : IBackendRegistry
{
    private readonly Dictionary<string, IVirtualDeviceBackend> _byId;
    private readonly List<IVirtualDeviceBackend> _ordered;

    /// <summary>Initialises the registry with the given backends, in registration order.</summary>
    /// <exception cref="ArgumentException">Thrown when two backends share the same id.</exception>
    public BackendRegistry(IEnumerable<IVirtualDeviceBackend> backends)
    {
        ArgumentNullException.ThrowIfNull(backends);

        _ordered = backends.ToList();
        _byId = new Dictionary<string, IVirtualDeviceBackend>(StringComparer.OrdinalIgnoreCase);

        foreach (var backend in _ordered)
        {
            if (!_byId.TryAdd(backend.Id, backend))
                throw new ArgumentException($"Duplicate virtual-device backend id: '{backend.Id}'.", nameof(backends));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IVirtualDeviceBackend> Backends => _ordered;

    /// <inheritdoc />
    public string DefaultBackendId =>
        _ordered.Count > 0
            ? _ordered[0].Id
            : throw new InvalidOperationException("No virtual-device backends are registered.");

    /// <inheritdoc />
    public IVirtualDeviceBackend? Find(string id) =>
        _byId.TryGetValue(id, out var backend) ? backend : null;

    /// <inheritdoc />
    public IVirtualDeviceBackend Resolve(string? id)
    {
        if (_ordered.Count == 0)
            throw new InvalidOperationException("No virtual-device backends are registered.");

        if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var backend))
            return backend;

        return _ordered[0];
    }
}
