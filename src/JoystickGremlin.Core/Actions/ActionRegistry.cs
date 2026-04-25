// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions;

/// <summary>
/// Thread-safe registry of <see cref="IActionDescriptor"/> instances keyed by tag.
/// </summary>
public sealed class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<string, IActionDescriptor> _descriptors = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public void Register(IActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (_lock)
            _descriptors[descriptor.Tag] = descriptor;
    }

    /// <inheritdoc/>
    public IActionDescriptor? Resolve(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        lock (_lock)
            return _descriptors.TryGetValue(tag, out var d) ? d : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IActionDescriptor> GetAll()
    {
        lock (_lock)
            return [.. _descriptors.Values];
    }
}
