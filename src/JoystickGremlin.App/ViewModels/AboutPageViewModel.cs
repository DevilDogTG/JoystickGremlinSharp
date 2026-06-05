// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Reactive;
using System.Reflection;
using JoystickGremlin.Core.Update;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the About page — displays application name, version,
/// repository link, license information, and the in-app update check.
/// </summary>
public sealed class AboutPageViewModel : ViewModelBase
{
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<AboutPageViewModel> _logger;
    private string _updateStatusMessage = string.Empty;
    private string? _updateDownloadUrl;
    private bool _isUpdateAvailable;

    /// <summary>
    /// Initializes a new instance of <see cref="AboutPageViewModel"/>.
    /// </summary>
    /// <param name="updateChecker">Service that queries GitHub Releases for a newer version.</param>
    /// <param name="logger">Logger for browser-launch failures.</param>
    public AboutPageViewModel(IUpdateChecker updateChecker, ILogger<AboutPageViewModel> logger)
    {
        _updateChecker = updateChecker;
        _logger = logger;

        OpenRepositoryCommand = ReactiveCommand.Create(() =>
            Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true }));
        OpenOriginalRepoCommand = ReactiveCommand.Create(() =>
            Process.Start(new ProcessStartInfo(OriginalRepoUrl) { UseShellExecute = true }));
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync);
        DownloadUpdateCommand = ReactiveCommand.Create(OpenDownloadPage,
            canExecute: this.WhenAnyValue(x => x.IsUpdateAvailable));
    }

    /// <summary>Gets the command that opens the GitHub repository in the default browser.</summary>
    public ReactiveCommand<Unit, Process?> OpenRepositoryCommand { get; }

    /// <summary>Gets the command that queries GitHub Releases for a newer version.</summary>
    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    /// <summary>Gets the command that opens the latest release download in the default browser.</summary>
    public ReactiveCommand<Unit, Unit> DownloadUpdateCommand { get; }

    /// <summary>Gets the status line below the update-check button (empty until the first check).</summary>
    public string UpdateStatusMessage
    {
        get => _updateStatusMessage;
        private set => this.RaiseAndSetIfChanged(ref _updateStatusMessage, value);
    }

    /// <summary>Gets a value indicating whether the last check found a newer release.</summary>
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value);
    }

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

    /// <summary>Gets the URL of the original JoystickGremlin project by WhiteMagic.</summary>
    public string OriginalRepoUrl => "https://github.com/WhiteMagic/JoystickGremlin";

    /// <summary>Gets the command that opens the original JoystickGremlin repository in the default browser.</summary>
    public ReactiveCommand<Unit, Process?> OpenOriginalRepoCommand { get; }

    /// <summary>Gets the license identifier.</summary>
    public string License => "GPL-3.0-only";

    /// <summary>Gets the copyright line.</summary>
    public string Copyright => "Copyright © 2024–2025 DevilDogTG";

    /// <summary>Gets the attribution text for the original JoystickGremlin project.</summary>
    public string Attribution =>
        "Based on JoystickGremlin by WhiteMagic. " +
        "The original Python implementation and DILL input library are the work of the original author and contributors. " +
        "This project would not exist without their foundational work.";

    /// <summary>Runs the update check and projects the result into the status properties.</summary>
    private async Task CheckForUpdatesAsync()
    {
        IsUpdateAvailable = false;
        _updateDownloadUrl = null;
        UpdateStatusMessage = "Checking for updates…";

        // The checker never throws on network/parse failures; those come back as Failed.
        var result = await _updateChecker.CheckForUpdateAsync();

        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                _updateDownloadUrl = result.DownloadUrl ?? result.ReleaseUrl;
                IsUpdateAvailable = true;
                UpdateStatusMessage =
                    $"Version {Format(result.LatestVersion)} is available — you have {Format(result.CurrentVersion)}.";
                break;

            case UpdateCheckStatus.UpToDate:
                UpdateStatusMessage = $"You're up to date — {Format(result.CurrentVersion)} is the latest version.";
                break;

            default:
                UpdateStatusMessage = $"Update check failed: {result.ErrorMessage}";
                break;
        }
    }

    /// <summary>Opens the latest release's installer download (or release page) in the default browser.</summary>
    private void OpenDownloadPage()
    {
        var url = _updateDownloadUrl;
        if (url is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open update download URL {Url}", url);
        }
    }

    /// <summary>Formats a version for the status line (<c>v12.1.0</c> style).</summary>
    /// <param name="version">Version to format; null renders as "an unknown version".</param>
    /// <returns>The display string.</returns>
    private static string Format(Version? version) => version is null ? "an unknown version" : $"v{version}";
}
