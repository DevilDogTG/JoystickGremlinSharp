// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.Core.Configuration;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.HidHide;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the HidHide integration settings page.
/// Lets users configure which physical devices to hide from other applications
/// while the Joystick Gremlin event pipeline is running.
/// </summary>
public sealed class HidHidePageViewModel : ViewModelBase, IDisposable
{
    private readonly IHidHideManager _hidHideManager;
    private readonly ISettingsService _settingsService;
    private readonly IDeviceManager _deviceManager;
    private readonly ILogger<HidHidePageViewModel> _logger;

    private bool _enableHidHide;
    private bool _autoHideOnPipelineRun;
    private bool _loading;
    private IDisposable? _settingsSubscription;

    /// <summary>
    /// Initializes a new instance of <see cref="HidHidePageViewModel"/>.
    /// </summary>
    public HidHidePageViewModel(
        IHidHideManager hidHideManager,
        ISettingsService settingsService,
        IDeviceManager deviceManager,
        ILogger<HidHidePageViewModel> logger)
    {
        _hidHideManager = hidHideManager;
        _settingsService = settingsService;
        _deviceManager = deviceManager;
        _logger = logger;

        DeviceRows = [];
        StaleDeviceRows = [];
        OwnExePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "(unknown)";
        StatusText = FormatStatus(_hidHideManager.Status, _hidHideManager.LastError);

        RefreshCommand   = ReactiveCommand.CreateFromTask(RefreshAsync);
        ApplyNowCommand  = ReactiveCommand.CreateFromTask(ApplyNowAsync);
        RevertNowCommand = ReactiveCommand.CreateFromTask(RevertNowAsync);

        _hidHideManager.StatusChanged += OnStatusChanged;

        // Auto-save settings when Enable or AutoHide properties change (debounced 500 ms).
        _settingsSubscription = this.WhenAnyValue(x => x.EnableHidHide, x => x.AutoHideOnPipelineRun, (_, _) => Unit.Default)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(500), AvaloniaScheduler.Instance)
            .Subscribe(unit =>
            {
                if (!_loading)
                    _ = SaveSettingsAsync();
            });
    }

    // ── Properties ───────────────────────────────────────────────────────────

    /// <summary>Gets the list of currently connected physical devices (with InstanceId resolved).</summary>
    public ObservableCollection<HidHideDeviceRowViewModel> DeviceRows { get; }

    /// <summary>Gets the list of remembered hidden devices that are not currently connected.</summary>
    public ObservableCollection<HidHideDeviceRowViewModel> StaleDeviceRows { get; }

    /// <summary>Gets the path of this application's own executable (whitelisted automatically).</summary>
    public string OwnExePath { get; }

    /// <summary>Gets a human-readable description of the current HidHide status.</summary>
    public string StatusText { get; private set; }

    /// <summary>Gets or sets whether HidHide integration is globally enabled.</summary>
    public bool EnableHidHide
    {
        get => _enableHidHide;
        set => this.RaiseAndSetIfChanged(ref _enableHidHide, value);
    }

    /// <summary>Gets or sets whether hiding is automatically triggered when the pipeline starts.</summary>
    public bool AutoHideOnPipelineRun
    {
        get => _autoHideOnPipelineRun;
        set => this.RaiseAndSetIfChanged(ref _autoHideOnPipelineRun, value);
    }

    /// <summary>Gets whether the HidHide driver is currently installed.</summary>
    public bool IsDriverInstalled => _hidHideManager.Status != HidHideStatus.NotInstalled;

    /// <summary>Gets a value indicating whether hiding is currently applied.</summary>
    public bool IsApplied => _hidHideManager.IsApplied;

    /// <summary>Gets the download link for HidHide.</summary>
    public string DownloadUrl => "https://github.com/nefarius/HidHide/releases/latest";

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Refreshes the device list and status.</summary>
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>Applies HidHide hiding immediately (manual override).</summary>
    public ReactiveCommand<Unit, Unit> ApplyNowCommand { get; }

    /// <summary>Reverts HidHide hiding immediately (manual override).</summary>
    public ReactiveCommand<Unit, Unit> RevertNowCommand { get; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads settings from <see cref="AppSettings"/> and refreshes the device list.
    /// Call after <see cref="ISettingsService.LoadAsync"/> has completed.
    /// </summary>
    public async Task LoadFromSettingsAsync()
    {
        _loading = true;
        try
        {
            var settings = _settingsService.Settings;
            EnableHidHide = settings.EnableHidHide;
            AutoHideOnPipelineRun = settings.AutoHideOnPipelineRun;

            await RefreshDeviceRowsAsync();
        }
        finally
        {
            _loading = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _settingsSubscription?.Dispose();
        _hidHideManager.StatusChanged -= OnStatusChanged;
        RefreshCommand.Dispose();
        ApplyNowCommand.Dispose();
        RevertNowCommand.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        await _hidHideManager.RefreshAsync();
        await RefreshDeviceRowsAsync();
        UpdateStatusText();
        this.RaisePropertyChanged(nameof(IsApplied));
        this.RaisePropertyChanged(nameof(IsDriverInstalled));
    }

    private async Task ApplyNowAsync()
    {
        await _hidHideManager.ApplyAsync();
        this.RaisePropertyChanged(nameof(IsApplied));
    }

    private async Task RevertNowAsync()
    {
        await _hidHideManager.RevertAsync();
        this.RaisePropertyChanged(nameof(IsApplied));
    }

    private Task RefreshDeviceRowsAsync()
    {
        var settings = _settingsService.Settings;

        // Connected devices with a resolvable InstanceId
        var connectedRows = _deviceManager.Devices
            .Where(d => d.InstanceId is not null)
            .Select(d =>
            {
                var isHidden = settings.HiddenDeviceInstanceIds.Contains(
                    d.InstanceId!, StringComparer.OrdinalIgnoreCase);
                var row = new HidHideDeviceRowViewModel(d.InstanceId!, d.Name, isHidden: isHidden);
                row.HideChanged += OnDeviceHideChanged;
                return row;
            })
            .ToList();

        // Stale entries: in settings but not connected
        var connectedIds = new HashSet<string>(
            connectedRows.Select(r => r.InstanceId),
            StringComparer.OrdinalIgnoreCase);

        var staleRows = settings.HiddenDevices
            .Where(e => !connectedIds.Contains(e.InstanceId))
            .Select(e =>
            {
                var row = new HidHideDeviceRowViewModel(e.InstanceId, e.FriendlyName, isHidden: true)
                {
                    IsStale = true
                };
                row.RemoveStaleRequested += OnRemoveStaleDevice;
                return row;
            })
            .ToList();

        Dispatcher.UIThread.Post(() =>
        {
            foreach (var row in DeviceRows)
                row.HideChanged -= OnDeviceHideChanged;
            DeviceRows.Clear();
            foreach (var row in connectedRows)
                DeviceRows.Add(row);

            foreach (var row in StaleDeviceRows)
                row.RemoveStaleRequested -= OnRemoveStaleDevice;
            StaleDeviceRows.Clear();
            foreach (var row in staleRows)
                StaleDeviceRows.Add(row);
        });

        return Task.CompletedTask;
    }

    private void OnDeviceHideChanged(object? sender, (string InstanceId, bool IsHidden, string FriendlyName) args)
    {
        var settings = _settingsService.Settings;

        if (args.IsHidden)
        {
            if (!settings.HiddenDeviceInstanceIds.Contains(args.InstanceId, StringComparer.OrdinalIgnoreCase))
            {
                settings.HiddenDeviceInstanceIds.Add(args.InstanceId);
                if (!settings.HiddenDevices.Any(d => string.Equals(d.InstanceId, args.InstanceId, StringComparison.OrdinalIgnoreCase)))
                    settings.HiddenDevices.Add(new HiddenDeviceEntry { InstanceId = args.InstanceId, FriendlyName = args.FriendlyName });
            }
        }
        else
        {
            settings.HiddenDeviceInstanceIds.RemoveAll(id =>
                string.Equals(id, args.InstanceId, StringComparison.OrdinalIgnoreCase));
            settings.HiddenDevices.RemoveAll(d =>
                string.Equals(d.InstanceId, args.InstanceId, StringComparison.OrdinalIgnoreCase));
        }

        _ = SaveSettingsAsync();
    }

    private void OnRemoveStaleDevice(object? sender, string instanceId)
    {
        var settings = _settingsService.Settings;
        settings.HiddenDeviceInstanceIds.RemoveAll(id =>
            string.Equals(id, instanceId, StringComparison.OrdinalIgnoreCase));
        settings.HiddenDevices.RemoveAll(d =>
            string.Equals(d.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));

        Dispatcher.UIThread.Post(() =>
        {
            var row = StaleDeviceRows.FirstOrDefault(r =>
                string.Equals(r.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                row.RemoveStaleRequested -= OnRemoveStaleDevice;
                StaleDeviceRows.Remove(row);
            }
        });

        _ = SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = _settingsService.Settings;
            settings.EnableHidHide = _enableHidHide;
            settings.AutoHideOnPipelineRun = _autoHideOnPipelineRun;
            await _settingsService.SaveAsync();
            _logger.LogDebug("HidHide settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save HidHide settings");
        }
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateStatusText();
            this.RaisePropertyChanged(nameof(IsApplied));
            this.RaisePropertyChanged(nameof(IsDriverInstalled));
        });
    }

    private void UpdateStatusText()
    {
        StatusText = FormatStatus(_hidHideManager.Status, _hidHideManager.LastError);
        this.RaisePropertyChanged(nameof(StatusText));
    }

    private static string FormatStatus(HidHideStatus status, string? lastError = null) => status switch
    {
        HidHideStatus.Disabled     => "Disabled — Enable HidHide integration above to activate",
        HidHideStatus.NotInstalled => "Not installed — HidHide driver not found",
        HidHideStatus.Ready        => "Ready — HidHide is installed and configured",
        HidHideStatus.Active       => "Active — Devices are currently hidden",
        HidHideStatus.Error        => string.IsNullOrEmpty(lastError)
                                        ? "Error — Check logs for details"
                                        : $"Error: {lastError}",
        _                          => status.ToString()
    };
}
