// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Actions;

/// <summary>
/// Manages registration and resolution of action descriptors by their string tag.
/// </summary>
public interface IActionRegistry
{
    /// <summary>
    /// Registers an action descriptor. Any existing descriptor with the same tag is replaced.
    /// </summary>
    /// <param name="descriptor">The action descriptor to register.</param>
    void Register(IActionDescriptor descriptor);

    /// <summary>
    /// Resolves an action descriptor by its tag.
    /// </summary>
    /// <param name="tag">The action tag to look up.</param>
    /// <returns>The registered descriptor, or <c>null</c> if not found.</returns>
    IActionDescriptor? Resolve(string tag);

    /// <summary>
    /// Returns all registered action descriptors.
    /// </summary>
    IReadOnlyList<IActionDescriptor> GetAll();
}
