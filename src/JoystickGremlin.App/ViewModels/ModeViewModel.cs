// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
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
    private int _depth;

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
    /// Gets or sets the depth of this mode in the hierarchy (0 = root, 1 = child, etc.).
    /// Set by <see cref="ProfilePageViewModel"/> during tree construction.
    /// </summary>
    public int Depth
    {
        get => _depth;
        set
        {
            if (_depth == value) return;
            this.RaiseAndSetIfChanged(ref _depth, value);
            this.RaisePropertyChanged(nameof(TreePadding));
        }
    }

    /// <summary>
    /// Gets the left padding for tree-style visual indentation in the mode list.
    /// Each level adds 14 px of left indent.
    /// </summary>
    public Thickness TreePadding => new Thickness(_depth * 14.0, 2, 4, 2);

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
