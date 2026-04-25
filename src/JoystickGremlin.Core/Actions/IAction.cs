// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Devices;

namespace JoystickGremlin.Core.Actions;

/// <summary>
/// Describes a configurable action that can be bound to a physical input.
/// </summary>
public interface IAction
{
    /// <summary>Gets the unique identifier tag for this action type (e.g., "map-to-vjoy").</summary>
    string Tag { get; }

    /// <summary>Gets the human-readable display name.</summary>
    string Name { get; }

    /// <summary>Gets the input types this action supports.</summary>
    IReadOnlyList<InputType> SupportedInputTypes { get; }

    /// <summary>Creates a new functor instance that executes this action at runtime.</summary>
    IFunctor CreateFunctor();
}

/// <summary>
/// Executes an action in response to an input event.
/// </summary>
public interface IFunctor
{
    /// <summary>
    /// Processes the given input event and performs the action's effect.
    /// </summary>
    Task ProcessAsync(Events.InputEvent inputEvent, CancellationToken cancellationToken = default);
}
