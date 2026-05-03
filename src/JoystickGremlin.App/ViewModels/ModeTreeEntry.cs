// SPDX-License-Identifier: GPL-3.0-only

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Represents a mode entry in the toolbar mode selector ComboBox, carrying both the
/// actual mode name (for switching) and an indented display label (for visual hierarchy).
/// </summary>
public sealed record ModeTreeEntry(string Name, string IndentedLabel);
