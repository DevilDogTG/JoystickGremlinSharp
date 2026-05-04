// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using JoystickGremlin.Core.Profile;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Represents a category or profile node in the Profile page tree view.
/// </summary>
public sealed class ProfileTreeNode
{
    /// <summary>
    /// Initializes a new instance of <see cref="ProfileTreeNode"/>.
    /// </summary>
    /// <param name="label">The text displayed for the node.</param>
    /// <param name="entry">The profile entry represented by this node, or <c>null</c> for category nodes.</param>
    public ProfileTreeNode(string label, ProfileEntry? entry = null)
    {
        Label = label;
        Entry = entry;
    }

    /// <summary>Gets the text displayed for the node.</summary>
    public string Label { get; }

    /// <summary>Gets the represented profile entry, or <c>null</c> for category nodes.</summary>
    public ProfileEntry? Entry { get; }

    /// <summary>Gets the child nodes for category nodes.</summary>
    public ObservableCollection<ProfileTreeNode> Children { get; } = [];
}
