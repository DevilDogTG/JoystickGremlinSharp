// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Exceptions;
using Microsoft.Extensions.Logging;
using ProfileModel = JoystickGremlin.Core.Profile.Profile;

namespace JoystickGremlin.Core.Modes;

/// <summary>
/// Stack-based mode manager supporting temporary mode overlays and parent-chain inheritance resolution.
/// </summary>
public sealed class ModeManager : IModeManager
{
    private readonly ILogger<ModeManager> _logger;

    /// <summary>Stack entries: (modeName, isTemporary). Bottom = base mode.</summary>
    private readonly Stack<(string Name, bool IsTemporary)> _stack = new();

    private readonly HashSet<string> _knownModes = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of <see cref="ModeManager"/>.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ModeManager(ILogger<ModeManager> logger)
    {
        _logger = logger;
        _stack.Push(("(none)", false));
    }

    /// <inheritdoc/>
    public string ActiveModeName => _stack.Peek().Name;

    /// <inheritdoc/>
    public event EventHandler<string>? ModeChanged;

    /// <inheritdoc/>
    public void Reset(ProfileModel profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _knownModes.Clear();
        foreach (var mode in profile.Modes)
            _knownModes.Add(mode.Name);

        _stack.Clear();
        var firstName = profile.Modes.Count > 0 ? profile.Modes[0].Name : "(none)";
        _stack.Push((firstName, false));

        _logger.LogTrace("Mode manager reset. Active mode: {Mode}", ActiveModeName);
        ModeChanged?.Invoke(this, ActiveModeName);
    }

    /// <inheritdoc/>
    public void SwitchTo(string modeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modeName);
        ThrowIfUnknown(modeName);

        // Clear all temporaries and replace base.
        _stack.Clear();
        _stack.Push((modeName, false));

        _logger.LogTrace("Switched to mode {Mode}", modeName);
        ModeChanged?.Invoke(this, modeName);
    }

    /// <inheritdoc/>
    public void PushTemporary(string modeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modeName);
        ThrowIfUnknown(modeName);

        _stack.Push((modeName, true));

        _logger.LogTrace("Pushed temporary mode {Mode}", modeName);
        ModeChanged?.Invoke(this, modeName);
    }

    /// <inheritdoc/>
    public bool PopTemporary()
    {
        if (_stack.Count <= 1 || !_stack.Peek().IsTemporary)
            return false;

        _stack.Pop();

        _logger.LogTrace("Popped temporary mode. Active mode: {Mode}", ActiveModeName);
        ModeChanged?.Invoke(this, ActiveModeName);
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetInheritanceChain(string modeName, ProfileModel profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modeName);
        ArgumentNullException.ThrowIfNull(profile);

        var chain = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = modeName;

        while (current is not null)
        {
            if (!visited.Add(current))
            {
                _logger.LogWarning("Circular mode inheritance detected at '{Mode}'. Breaking chain.", current);
                break;
            }

            chain.Add(current);
            var mode = profile.Modes.FirstOrDefault(m => string.Equals(m.Name, current, StringComparison.Ordinal));
            current = mode?.ParentModeName;
        }

        return chain;
    }

    private void ThrowIfUnknown(string modeName)
    {
        if (_knownModes.Count > 0 && !_knownModes.Contains(modeName))
            throw new ModeException($"Mode '{modeName}' is not defined in the current profile.");
    }
}
