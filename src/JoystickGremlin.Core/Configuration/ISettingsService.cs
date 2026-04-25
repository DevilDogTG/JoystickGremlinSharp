// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Configuration;

/// <summary>
/// Loads, holds, and persists <see cref="AppSettings"/> to a JSON file.
/// </summary>
public interface ISettingsService
{
    /// <summary>Gets the current application settings. Always non-null after <see cref="LoadAsync"/>.</summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Loads settings from disk, or initialises defaults if the file does not exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the current <see cref="Settings"/> to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
