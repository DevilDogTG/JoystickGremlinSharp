// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json.Nodes;
using JoystickGremlin.Core.Events;

namespace JoystickGremlin.Core.Actions;

/// <summary>
/// Executes a single action in response to a physical input event.
/// </summary>
public interface IActionFunctor
{
    /// <summary>
    /// Executes the action using the provided input event context.
    /// </summary>
    /// <param name="inputEvent">The triggering input event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(InputEvent inputEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a registerable action type including its metadata and functor factory.
/// </summary>
public interface IActionDescriptor
{
    /// <summary>Gets the unique string tag that identifies this action type.</summary>
    string Tag { get; }

    /// <summary>Gets the human-readable display name of the action type.</summary>
    string Name { get; }

    /// <summary>
    /// Creates a configured functor for this action from the provided JSON configuration.
    /// </summary>
    /// <param name="configuration">Serialized action configuration, or null for defaults.</param>
    /// <returns>A ready-to-use <see cref="IActionFunctor"/>.</returns>
    IActionFunctor CreateFunctor(JsonObject? configuration);
}
