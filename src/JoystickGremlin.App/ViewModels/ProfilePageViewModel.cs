// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Profile page — browses and manages the profile library
/// (create, delete, rename profiles organised in category subfolders).
/// </summary>
public sealed class ProfilePageViewModel : ViewModelBase, IDisposable
{
    private readonly IProfileLibrary _profileLibrary;
    private readonly ILogger<ProfilePageViewModel> _logger;

    private ProfileEntry? _selectedEntry;
    private ProfileTreeNode? _selectedNode;
    private string _newProfileName = string.Empty;
    private string? _selectedCategory;
    private string? _preferredSelectedCategory;
    private string? _preferredSelectedName;

    /// <summary>
    /// Initializes a new instance of <see cref="ProfilePageViewModel"/>.
    /// </summary>
    public ProfilePageViewModel(IProfileLibrary profileLibrary, ILogger<ProfilePageViewModel> logger)
    {
        _profileLibrary = profileLibrary;
        _logger         = logger;

        ProfileTree = new ObservableCollection<ProfileTreeNode>();

        var hasSelection = this.WhenAnyValue(x => x.SelectedEntry).Select(e => e is not null);
        var hasName      = this.WhenAnyValue(x => x.NewProfileName).Select(n => !string.IsNullOrWhiteSpace(n));

        SubmitProfileCommand     = ReactiveCommand.CreateFromTask(SubmitProfileAsync, hasName);
        DeleteProfileCommand     = ReactiveCommand.CreateFromTask(DeleteProfileAsync, hasSelection);
        OpenProfileFolderCommand = ReactiveCommand.Create(OpenProfileFolder);
        RefreshCommand           = ReactiveCommand.CreateFromTask(() => _profileLibrary.ScanAsync());

        _profileLibrary.LibraryChanged += OnLibraryChanged;
    }

    /// <summary>Gets the tree of discovered categories and profiles.</summary>
    public ObservableCollection<ProfileTreeNode> ProfileTree { get; }

    /// <summary>Gets or sets the currently selected tree node.</summary>
    public ProfileTreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            SelectedEntry = value?.Entry;
            ApplySelectionToEditor(value);
        }
    }

    /// <summary>Gets or sets the currently selected profile entry.</summary>
    public ProfileEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedEntry, value);
    }

    /// <summary>Gets or sets the name typed into the New Profile text box.</summary>
    public string NewProfileName
    {
        get => _newProfileName;
        set => this.RaiseAndSetIfChanged(ref _newProfileName, value);
    }

    /// <summary>Gets or sets the optional category (subfolder) for new profiles.</summary>
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set => this.RaiseAndSetIfChanged(ref _selectedCategory, value);
    }

    /// <summary>Gets a value indicating whether the form is editing an existing profile.</summary>
    public bool IsEditMode => SelectedEntry is not null;

    /// <summary>Gets the form heading for the create/edit panel.</summary>
    public string EditorTitle => IsEditMode ? "Edit Profile" : "Create New Profile";

    /// <summary>Gets the label for the primary action button.</summary>
    public string SubmitButtonLabel => IsEditMode ? "Update" : "+ Create";

    /// <summary>Gets the path to the profiles folder currently in use.</summary>
    public string ProfilesFolderPath => _profileLibrary.ProfilesFolder;

    /// <summary>Gets the command that creates a new profile or updates the selected profile name.</summary>
    public ReactiveCommand<Unit, Unit> SubmitProfileCommand { get; }

    /// <summary>Gets the command that deletes the selected profile file.</summary>
    public ReactiveCommand<Unit, Unit> DeleteProfileCommand { get; }

    /// <summary>Gets the command that opens the profiles folder in Windows Explorer.</summary>
    public ReactiveCommand<Unit, Unit> OpenProfileFolderCommand { get; }

    /// <summary>Gets the command that rescans the profiles folder and refreshes the library.</summary>
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>
    /// Scans the profile library and populates <see cref="ProfileEntries"/>.
    /// Call after initialization.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _profileLibrary.ScanAsync();
    }

    private async Task SubmitProfileAsync()
    {
        if (IsEditMode)
        {
            await UpdateProfileAsync();
            return;
        }

        await CreateProfileAsync();
    }

    private async Task CreateProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName)) return;
        try
        {
            await _profileLibrary.CreateProfileAsync(NewProfileName.Trim(), SelectedCategory);
            NewProfileName = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create profile '{Name}'", NewProfileName);
        }
    }

    private async Task UpdateProfileAsync()
    {
        if (SelectedEntry is null || string.IsNullOrWhiteSpace(NewProfileName))
            return;

        try
        {
            _preferredSelectedCategory = SelectedEntry.Category;
            _preferredSelectedName = NewProfileName.Trim();
            await _profileLibrary.RenameProfileAsync(SelectedEntry.FilePath, _preferredSelectedName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename profile '{Path}'", SelectedEntry.FilePath);
        }
    }

    private async Task DeleteProfileAsync()
    {
        if (SelectedEntry is null) return;
        try
        {
            await _profileLibrary.DeleteProfileAsync(SelectedEntry.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete profile '{Path}'", SelectedEntry?.FilePath);
        }
    }

    private void OpenProfileFolder()
    {
        try
        {
            var folder = _profileLibrary.ProfilesFolder;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open profiles folder");
        }
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var prev = SelectedEntry?.FilePath;
            RebuildTree(prev);
            this.RaisePropertyChanged(nameof(ProfilesFolderPath));
        });
    }

    private void RebuildTree(string? selectedFilePath)
    {
        ProfileTree.Clear();

        foreach (var entry in _profileLibrary.Entries.Where(e => e.Category is null))
            ProfileTree.Add(new ProfileTreeNode(entry.Name, entry));

        foreach (var categoryGroup in _profileLibrary.Entries
                     .Where(e => e.Category is not null)
                     .GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var categoryNode = new ProfileTreeNode(categoryGroup.Key!);
            foreach (var entry in categoryGroup.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                categoryNode.Children.Add(new ProfileTreeNode(entry.Name, entry));

            ProfileTree.Add(categoryNode);
        }

        SelectedNode = FindNodeByEntry(ProfileTree, _preferredSelectedCategory, _preferredSelectedName)
            ?? FindNodeByPath(ProfileTree, selectedFilePath)
            ?? ProfileTree.SelectMany(FlattenNodes).FirstOrDefault(node => node.Entry is not null);

        _preferredSelectedCategory = null;
        _preferredSelectedName = null;
    }

    private static ProfileTreeNode? FindNodeByPath(IEnumerable<ProfileTreeNode> nodes, string? filePath)
    {
        if (filePath is null)
            return null;

        return nodes.SelectMany(FlattenNodes)
            .FirstOrDefault(node => string.Equals(node.Entry?.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private static ProfileTreeNode? FindNodeByEntry(
        IEnumerable<ProfileTreeNode> nodes,
        string? category,
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return nodes.SelectMany(FlattenNodes)
            .FirstOrDefault(node =>
                string.Equals(node.Entry?.Category, category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(node.Entry?.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplySelectionToEditor(ProfileTreeNode? selectedNode)
    {
        if (selectedNode?.Entry is not null)
        {
            NewProfileName = selectedNode.Entry.Name;
            SelectedCategory = selectedNode.Entry.Category;
        }
        else if (selectedNode?.Entry is null && selectedNode is not null && selectedNode.Children.Count > 0)
        {
            NewProfileName = string.Empty;
            SelectedCategory = selectedNode.Label;
        }

        this.RaisePropertyChanged(nameof(IsEditMode));
        this.RaisePropertyChanged(nameof(EditorTitle));
        this.RaisePropertyChanged(nameof(SubmitButtonLabel));
    }

    private static IEnumerable<ProfileTreeNode> FlattenNodes(ProfileTreeNode node)
    {
        yield return node;

        foreach (var child in node.Children.SelectMany(FlattenNodes))
            yield return child;
    }

    /// <inheritdoc/>
    public void Dispose() => _profileLibrary.LibraryChanged -= OnLibraryChanged;
}
