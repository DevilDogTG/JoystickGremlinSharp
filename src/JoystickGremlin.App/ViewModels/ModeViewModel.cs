// SPDX-License-Identifier: GPL-3.0-only

using JoystickGremlin.Core.Profile;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel wrapping a single <see cref="Mode"/> for display and inline editing on the Profile page.
/// </summary>
public sealed class ModeViewModel : ViewModelBase
{
    private string _name;
    private string? _parentModeName;

    /// <summary>
    /// Initializes a new instance of <see cref="ModeViewModel"/>.
    /// </summary>
    /// <param name="mode">The underlying mode model.</param>
    public ModeViewModel(Mode mode)
    {
        Model = mode;
        _name = mode.Name;
        _parentModeName = mode.ParentModeName;
    }

    /// <summary>Gets the underlying <see cref="Mode"/> model.</summary>
    public Mode Model { get; }

    /// <summary>Gets or sets the mode name.</summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>Gets or sets the parent mode name, or <c>null</c> for a root mode.</summary>
    public string? ParentModeName
    {
        get => _parentModeName;
        set => this.RaiseAndSetIfChanged(ref _parentModeName, value);
    }

    /// <summary>Gets a value indicating whether this mode has no parent (root mode).</summary>
    public bool IsRoot => string.IsNullOrEmpty(ParentModeName);

    /// <summary>
    /// Applies the current ViewModel state back to the underlying <see cref="Mode"/> model.
    /// Call after the user confirms an edit.
    /// </summary>
    public void CommitToModel()
    {
        Model.Name = _name;
        Model.ParentModeName = string.IsNullOrWhiteSpace(_parentModeName) ? null : _parentModeName;
    }
}
