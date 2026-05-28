// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using JoystickGremlin.Core.ProcessMonitor;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the process picker dialog. Enumerates running processes on a background thread,
/// supports text filtering and a "show all processes" toggle, and exposes the user's selection.
/// </summary>
public sealed class ProcessPickerViewModel : ViewModelBase
{
    private readonly IProcessEnumerator _enumerator;
    private IReadOnlyList<RunningProcessInfo> _all = [];
    private string _searchText = string.Empty;
    private bool _showAllProcesses;
    private bool _isLoading;
    private RunningProcessInfo? _selectedProcess;

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessPickerViewModel"/> and starts the initial load.
    /// </summary>
    public ProcessPickerViewModel(IProcessEnumerator enumerator)
    {
        _enumerator = enumerator;
        Processes = [];
        RefreshCommand = ReactiveCommand.CreateFromTask(ReloadAsync);

        this.WhenAnyValue(x => x.SearchText)
            .Subscribe(_ => ApplyFilter());

        this.WhenAnyValue(x => x.ShowAllProcesses)
            .Skip(1)
            .Subscribe(value => { _ = ReloadAsync(); });

        _ = ReloadAsync();
    }

    /// <summary>Gets the filtered list of running processes shown in the dialog.</summary>
    public ObservableCollection<RunningProcessInfo> Processes { get; }

    /// <summary>Gets or sets the free-text filter applied to the process list.</summary>
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    /// <summary>Gets or sets whether all processes (not just windowed apps) are listed.</summary>
    public bool ShowAllProcesses
    {
        get => _showAllProcesses;
        set => this.RaiseAndSetIfChanged(ref _showAllProcesses, value);
    }

    /// <summary>Gets a value indicating whether enumeration is currently running.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>Gets or sets the currently selected process, or <c>null</c> if none.</summary>
    public RunningProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set => this.RaiseAndSetIfChanged(ref _selectedProcess, value);
    }

    /// <summary>Gets the command that re-enumerates running processes.</summary>
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    private async Task ReloadAsync()
    {
        IsLoading = true;
        var includeAll = ShowAllProcesses;
        try
        {
            _all = await Task.Run(() => _enumerator.GetUserProcesses(includeAll));
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyFilter();
                IsLoading = false;
            });
        }
    }

    private void ApplyFilter()
    {
        var previous = SelectedProcess;
        var query = SearchText?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(p =>
                p.ExecutableName.Contains(query, System.StringComparison.OrdinalIgnoreCase)
                || p.WindowTitle.Contains(query, System.StringComparison.OrdinalIgnoreCase)
                || p.ProcessName.Contains(query, System.StringComparison.OrdinalIgnoreCase)
                || p.ExecutablePath.Contains(query, System.StringComparison.OrdinalIgnoreCase))
              .ToList();

        Processes.Clear();
        foreach (var p in filtered)
            Processes.Add(p);

        // Preserve the selection if it is still present.
        if (previous is not null)
            SelectedProcess = Processes.FirstOrDefault(p => p.Pid == previous.Pid);
    }
}
