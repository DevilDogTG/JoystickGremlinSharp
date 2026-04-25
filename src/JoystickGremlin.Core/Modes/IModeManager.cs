// SPDX-License-Identifier: GPL-3.0-only

using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Modes;

/// <summary>
/// Manages the active mode and a temporary mode stack for a running profile.
/// </summary>
public interface IModeManager
{
    /// <summary>Gets the name of the currently active mode.</summary>
    string ActiveModeName { get; }

    /// <summary>Raised when the active mode changes. The event argument is the new mode name.</summary>
    event EventHandler<string>? ModeChanged;

    /// <summary>
    /// Initialises the mode manager from the loaded profile, setting the active mode to the first mode defined.
    /// </summary>
    /// <param name="profile">The profile whose modes will be used.</param>
    void Reset(ProfileModel profile);

    /// <summary>
    /// Switches the base (non-temporary) active mode to <paramref name="modeName"/>,
    /// clearing any temporary modes on the stack.
    /// </summary>
    /// <param name="modeName">The target mode name. Must exist in the current profile.</param>
    /// <exception cref="Exceptions.ModeException">Thrown when the mode name is not found.</exception>
    void SwitchTo(string modeName);

    /// <summary>
    /// Pushes a temporary mode onto the stack. The previous mode is restored when <see cref="PopTemporary"/> is called.
    /// </summary>
    /// <param name="modeName">The temporary mode name. Must exist in the current profile.</param>
    /// <exception cref="Exceptions.ModeException">Thrown when the mode name is not found.</exception>
    void PushTemporary(string modeName);

    /// <summary>
    /// Pops the top temporary mode from the stack, restoring the previous mode.
    /// </summary>
    /// <returns><c>true</c> if a temporary mode was popped; <c>false</c> if the stack had only the base mode.</returns>
    bool PopTemporary();

    /// <summary>
    /// Returns the inheritance chain for a given mode name, from most-specific to least-specific (root last).
    /// Used by the event pipeline to resolve bindings through mode inheritance.
    /// </summary>
    /// <param name="modeName">The mode name to resolve.</param>
    /// <param name="profile">The profile containing mode definitions.</param>
    /// <returns>An ordered list of mode names, starting with <paramref name="modeName"/> up to the root.</returns>
    IReadOnlyList<string> GetInheritanceChain(string modeName, ProfileModel profile);
}
