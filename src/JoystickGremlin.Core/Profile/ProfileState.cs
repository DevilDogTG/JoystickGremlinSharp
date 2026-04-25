// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.Core.Profile;

/// <summary>
/// Default implementation of <see cref="IProfileState"/>. Registered as a Singleton.
/// </summary>
public sealed class ProfileState : IProfileState
{
    private Profile? _currentProfile;
    private string? _filePath;

    /// <inheritdoc/>
    public Profile? CurrentProfile => _currentProfile;

    /// <inheritdoc/>
    public string? FilePath => _filePath;

    /// <inheritdoc/>
    public event EventHandler<Profile?>? ProfileChanged;

    /// <inheritdoc/>
    public event EventHandler<string?>? FilePathChanged;

    /// <inheritdoc/>
    public void SetProfile(Profile profile, string? filePath = null)
    {
        _currentProfile = profile;
        if (filePath != _filePath)
        {
            _filePath = filePath;
            FilePathChanged?.Invoke(this, filePath);
        }
        ProfileChanged?.Invoke(this, profile);
    }

    /// <inheritdoc/>
    public void UpdateFilePath(string? filePath)
    {
        if (filePath == _filePath) return;
        _filePath = filePath;
        FilePathChanged?.Invoke(this, filePath);
    }

    /// <inheritdoc/>
    public void NotifyProfileModified() => ProfileChanged?.Invoke(this, _currentProfile);

    /// <inheritdoc/>
    public void ClearProfile()
    {
        _currentProfile = null;
        if (_filePath is not null)
        {
            _filePath = null;
            FilePathChanged?.Invoke(this, null);
        }
        ProfileChanged?.Invoke(this, null);
    }
}
