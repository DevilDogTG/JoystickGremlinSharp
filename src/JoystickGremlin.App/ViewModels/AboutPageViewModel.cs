// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the About page — displays application name, version,
/// repository link, and license information.
/// </summary>
public sealed class AboutPageViewModel : ViewModelBase
{
    /// <summary>Gets the application display name.</summary>
    public string AppName => "Joystick Gremlin Sharp";

    /// <summary>Gets the application version read from the entry assembly.</summary>
    public string Version
    {
        get
        {
            var ver = Assembly.GetEntryAssembly()?.GetName().Version;
            return ver is null ? "unknown" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
        }
    }

    /// <summary>Gets the full version string including label prefix.</summary>
    public string VersionLabel => $"Version {Version}";

    /// <summary>Gets the GitHub repository URL.</summary>
    public string RepositoryUrl => "https://github.com/DevilDogTG/JoystickGremlinSharp";

    /// <summary>Gets the display text for the repository link.</summary>
    public string RepositoryDisplayText => "github.com/DevilDogTG/JoystickGremlinSharp";

    /// <summary>Gets the license identifier.</summary>
    public string License => "GPL-3.0-only";

    /// <summary>Gets the copyright line.</summary>
    public string Copyright => "Copyright © 2024–2025 DevilDogTG";

    /// <summary>Gets the attribution text for the original JoystickGremlin project.</summary>
    public string Attribution =>
        "Based on JoystickGremlin by WhiteMagic. " +
        "The original Python implementation and DILL input library are the work of the original author and contributors.";
}
