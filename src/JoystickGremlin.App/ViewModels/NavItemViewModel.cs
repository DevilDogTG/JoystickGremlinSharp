// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Represents a single item in the main window's navigation sidebar.
/// </summary>
public sealed class NavItemViewModel : ViewModelBase
{
    /// <summary>Gets the display label for this navigation item.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the icon text (emoji or Unicode glyph) shown above the label.</summary>
    public required string Icon { get; init; }

    /// <summary>Gets the page ViewModel that is shown when this nav item is selected.</summary>
    public required ViewModelBase Page { get; init; }
}
