// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Provides load and save operations for <see cref="Profile"/> instances using JSON serialization.
/// </summary>
public interface IProfileRepository
{
    /// <summary>
    /// Loads a profile from the specified file path.
    /// </summary>
    /// <param name="filePath">Absolute path to the profile JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized <see cref="Profile"/>.</returns>
    /// <exception cref="ProfileException">Thrown when the file cannot be read or deserialized.</exception>
    Task<Profile> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a profile to the specified file path, creating directories as needed.
    /// </summary>
    /// <param name="profile">The profile to serialize.</param>
    /// <param name="filePath">Absolute path where the profile JSON file will be written.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ProfileException">Thrown when the file cannot be written.</exception>
    Task SaveAsync(Profile profile, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the profile from the specified path, or returns a new empty profile if the file does not exist.
    /// </summary>
    /// <param name="filePath">Absolute path to the profile JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or newly created <see cref="Profile"/>.</returns>
    Task<Profile> LoadOrCreateAsync(string filePath, CancellationToken cancellationToken = default);
}
