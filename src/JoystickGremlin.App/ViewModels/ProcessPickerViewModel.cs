// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
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
    private CancellationTokenSource? _reloadCts;
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

    /// <summary>
    /// Enumerates processes off the UI thread and commits the result, cancelling any in-flight
    /// reload first so a fast user toggle of <see cref="ShowAllProcesses"/> can't have a slow
    /// previous enumeration overwrite a newer one's result.
    /// </summary>
    private async Task ReloadAsync()
    {
        // Cancel and dispose any prior reload; this property setter is only ever entered from
        // the UI thread, so the field swap doesn't need Interlocked.
        var previousCts = _reloadCts;
        _reloadCts = new CancellationTokenSource();
        previousCts?.Cancel();
        previousCts?.Dispose();
        var token = _reloadCts.Token;

        IsLoading = true;
        var includeAll = ShowAllProcesses;
        try
        {
            var result = await Task.Run(() => _enumerator.GetUserProcesses(includeAll), token);
            if (token.IsCancellationRequested)
            {
                return;
            }
            _all = result;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                ApplyFilter();
                IsLoading = false;
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later reload — drop the result silently.
        }
    }

    /// <summary>
    /// Rebuilds <see cref="Processes"/> from the cached enumeration in <c>_all</c>, applying the
    /// current <see cref="SearchText"/> filter and preserving the selected process if still present.
    /// </summary>
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
        {
            Processes.Add(p);
        }

        // Preserve the selection if it is still present.
        if (previous is not null)
        {
            SelectedProcess = Processes.FirstOrDefault(p => p.Pid == previous.Pid);
        }
    }
}
