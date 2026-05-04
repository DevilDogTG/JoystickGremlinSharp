// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using JoystickGremlin.App.ViewModels.InputViewer;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Events;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the merged controller setup page.
/// Combines device browsing, live input inspection, and binding editing in one screen.
/// </summary>
public sealed class ControllerSetupPageViewModel : ViewModelBase, IDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly IActionRegistry _actionRegistry;
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileState _profileState;
    private readonly ILogger<ControllerSetupPageViewModel> _logger;
    private readonly ConcurrentDictionary<Guid, DeviceLiveInputViewModel> _liveDevices = new();
    private readonly CompositeDisposable _subscriptions = [];

    private DeviceViewModel? _selectedDevice;
    private UnifiedInputRowViewModel? _selectedInputRow;
    private bool _isBindingEditorOpen;
    private bool _isRebuildingInputRows;

    /// <summary>
    /// Initializes a new instance of <see cref="ControllerSetupPageViewModel"/>.
    /// </summary>
    public ControllerSetupPageViewModel(
        IDeviceManager deviceManager,
        IActionRegistry actionRegistry,
        IProfileRepository profileRepository,
        IProfileState profileState,
        BindingsPageViewModel bindingEditor,
        ILogger<ControllerSetupPageViewModel> logger)
    {
        _deviceManager = deviceManager;
        _actionRegistry = actionRegistry;
        _profileRepository = profileRepository;
        _profileState = profileState;
        BindingEditor = bindingEditor;
        _logger = logger;

        Devices = [];
        InputRows = [];

        OpenBindingEditorCommand = ReactiveCommand.Create(OpenBindingEditor);
        CloseBindingEditorCommand = ReactiveCommand.Create(CloseBindingEditor);
        SaveBindingChangesCommand = ReactiveCommand.CreateFromTask(SaveBindingChangesAsync);

        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;
        _deviceManager.InputReceived += OnInputReceived;
        _profileState.ProfileChanged += OnProfileChanged;

        _subscriptions.Add(this.WhenAnyValue(x => x.SelectedDevice)
            .Subscribe(OnSelectedDeviceChanged));

        _subscriptions.Add(this.WhenAnyValue(x => x.SelectedInputRow)
            .Subscribe(OnSelectedInputRowChanged));
    }

    /// <summary>Gets the device list shown in the left panel.</summary>
    public ObservableCollection<DeviceViewModel> Devices { get; }

    /// <summary>Gets the current input rows for the selected device.</summary>
    public ObservableCollection<UnifiedInputRowViewModel> InputRows { get; }

    /// <summary>Gets the binding editor state used by the overlay panel.</summary>
    public BindingsPageViewModel BindingEditor { get; }

    /// <summary>Gets or sets the selected physical device.</summary>
    public DeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    /// <summary>Gets or sets the selected input row.</summary>
    public UnifiedInputRowViewModel? SelectedInputRow
    {
        get => _selectedInputRow;
        set => this.RaiseAndSetIfChanged(ref _selectedInputRow, value);
    }

    /// <summary>Gets or sets whether the binding editor overlay is open.</summary>
    public bool IsBindingEditorOpen
    {
        get => _isBindingEditorOpen;
        set => this.RaiseAndSetIfChanged(ref _isBindingEditorOpen, value);
    }

    /// <summary>Gets whether an input row is currently selected.</summary>
    public bool HasSelectedInput => SelectedInputRow is not null;

    /// <summary>Gets whether the selected input is an axis.</summary>
    public bool ShowAxisDetail => SelectedInputRow?.IsAxis ?? false;

    /// <summary>Gets whether the selected input is a button.</summary>
    public bool ShowButtonDetail => SelectedInputRow?.IsButton ?? false;

    /// <summary>Gets whether the selected input is a hat.</summary>
    public bool ShowHatDetail => SelectedInputRow?.IsHat ?? false;

    /// <summary>Gets whether the selected input can currently be edited.</summary>
    public bool CanEditBinding => SelectedInputRow is not null && BindingEditor.HasProfile;

    /// <summary>Gets the behavior options used by the keyboard mapping editor.</summary>
    public IReadOnlyList<string> MapToKeyboardBehaviors => BindingsPageViewModel.MapToKeyboardBehaviors;

    /// <summary>Gets the command that opens the binding editor overlay.</summary>
    public ReactiveCommand<Unit, Unit> OpenBindingEditorCommand { get; }

    /// <summary>Gets the command that closes the binding editor overlay.</summary>
    public ReactiveCommand<Unit, Unit> CloseBindingEditorCommand { get; }

    /// <summary>Gets the command that saves the current binding editor changes.</summary>
    public ReactiveCommand<Unit, Unit> SaveBindingChangesCommand { get; }

    /// <summary>
    /// Rebuilds the device and live-input state from the currently connected devices.
    /// Call after <see cref="IDeviceManager.Initialize"/> completes.
    /// </summary>
    public void RefreshDevices()
    {
        Dispatcher.UIThread.Post(() =>
        {
            DisposeInputRows();
            DisposeLiveDevices();

            Devices.Clear();

            foreach (var device in _deviceManager.Devices)
            {
                Devices.Add(new DeviceViewModel(device));
                _liveDevices[device.Guid] = new DeviceLiveInputViewModel(device);
            }

            BindingEditor.RefreshDevices();
            SelectedDevice = Devices.FirstOrDefault();

            Dispatcher.UIThread.Post(SyncBindingEditorSelection);
        });
    }

    private void OnSelectedDeviceChanged(DeviceViewModel? device)
    {
        if (device is null)
        {
            DisposeInputRows();
            InputRows.Clear();
            SelectedInputRow = null;
            SyncBindingEditorSelection();
            return;
        }

        RebuildInputRows();
        SyncBindingEditorSelection();
    }

    private void OnSelectedInputRowChanged(UnifiedInputRowViewModel? row)
    {
        this.RaisePropertyChanged(nameof(HasSelectedInput));
        this.RaisePropertyChanged(nameof(ShowAxisDetail));
        this.RaisePropertyChanged(nameof(ShowButtonDetail));
        this.RaisePropertyChanged(nameof(ShowHatDetail));
        this.RaisePropertyChanged(nameof(CanEditBinding));

        if (row is null)
        {
            if (!_isRebuildingInputRows)
                IsBindingEditorOpen = false;

            if (_isRebuildingInputRows)
                return;
        }

        SyncBindingEditorSelection();
    }

    private void OpenBindingEditor()
    {
        if (CanEditBinding)
            IsBindingEditorOpen = true;
    }

    private void CloseBindingEditor()
    {
        IsBindingEditorOpen = false;
    }

    private async Task SaveBindingChangesAsync()
    {
        if (SelectedInputRow is not null && BindingEditor.SelectedBoundAction is not null)
        {
            BindingEditor.SaveCurrentActionConfig();
            RefreshSelectedInputRowSummary();
        }

        var profile = _profileState.CurrentProfile;
        var filePath = _profileState.FilePath;

        if (profile is null || string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("Cannot persist binding changes because no profile file is loaded.");
            return;
        }

        await _profileRepository.SaveAsync(profile, filePath);
        _logger.LogInformation("Saved profile {ProfileName} to {Path}", profile.Name, filePath);
    }

    private void RebuildInputRows()
    {
        (InputType inputType, int identifier)? previousSelection = SelectedInputRow is null
            ? null
            : (SelectedInputRow.InputType, SelectedInputRow.Identifier);
        var shouldKeepBindingEditorOpen = IsBindingEditorOpen;

        _isRebuildingInputRows = true;

        try
        {
            DisposeInputRows();
            InputRows.Clear();

            if (SelectedDevice?.Device is null)
            {
                SelectedInputRow = null;
                return;
            }

            var device = SelectedDevice.Device;
            if (!_liveDevices.TryGetValue(device.Guid, out var liveDevice))
            {
                liveDevice = new DeviceLiveInputViewModel(device);
                _liveDevices[device.Guid] = liveDevice;
            }

            for (var i = 1; i <= device.AxisCount; i++)
                InputRows.Add(CreateRow(device.Guid, InputType.JoystickAxis, i, liveDevice));

            for (var i = 1; i <= device.ButtonCount; i++)
                InputRows.Add(CreateRow(device.Guid, InputType.JoystickButton, i, liveDevice));

            for (var i = 1; i <= device.HatCount; i++)
                InputRows.Add(CreateRow(device.Guid, InputType.JoystickHat, i, liveDevice));

            SelectedInputRow = previousSelection is { } selection
                ? InputRows.FirstOrDefault(row =>
                    row.InputType == selection.inputType &&
                    row.Identifier == selection.identifier)
                : InputRows.FirstOrDefault();
        }
        finally
        {
            _isRebuildingInputRows = false;
            if (shouldKeepBindingEditorOpen && SelectedInputRow is not null)
                IsBindingEditorOpen = true;
        }
    }

    private UnifiedInputRowViewModel CreateRow(Guid deviceGuid, InputType inputType, int identifier, DeviceLiveInputViewModel liveDevice)
    {
        var row = inputType switch
        {
            InputType.JoystickAxis => new UnifiedInputRowViewModel(
                inputType,
                identifier,
                axis: liveDevice.Axes.FirstOrDefault(axis => axis.AxisIndex == identifier)),
            InputType.JoystickButton => new UnifiedInputRowViewModel(
                inputType,
                identifier,
                button: liveDevice.Buttons.FirstOrDefault(button => button.ButtonIndex == identifier)),
            InputType.JoystickHat => new UnifiedInputRowViewModel(
                inputType,
                identifier,
                hat: liveDevice.Hats.FirstOrDefault(hat => hat.HatIndex == identifier)),
            _ => new UnifiedInputRowViewModel(inputType, identifier),
        };

        row.BoundActions = BuildBoundActionsSummary(deviceGuid, inputType, identifier);
        return row;
    }

    private void RefreshSelectedInputRowSummary()
    {
        if (SelectedDevice is null || SelectedInputRow is null)
            return;

        SelectedInputRow.BoundActions = BuildBoundActionsSummary(
            SelectedDevice.Device.Guid,
            SelectedInputRow.InputType,
            SelectedInputRow.Identifier);
    }

    private string BuildBoundActionsSummary(Guid deviceGuid, InputType inputType, int identifier)
    {
        var profile = _profileState.CurrentProfile;
        if (profile is null)
            return "(none)";

        var binding = profile.Bindings.FirstOrDefault(b =>
            b.DeviceGuid == deviceGuid &&
            b.InputType == inputType &&
            b.Identifier == identifier);

        if (binding is null || binding.Actions.Count == 0)
            return "(none)";

        var actionNames = binding.Actions
            .Select(action => _actionRegistry.Resolve(action.ActionTag)?.Name ?? action.ActionTag)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return actionNames.Count > 0
            ? string.Join(", ", actionNames)
            : "(none)";
    }

    private void SyncBindingEditorSelection()
    {
        if (SelectedDevice is null)
        {
            BindingEditor.SelectedDevice = null;
            BindingEditor.SelectedInput = null;
            return;
        }

        BindingEditor.SelectedDevice = BindingEditor.Devices.FirstOrDefault(
            device => device.Device.Guid == SelectedDevice.Device.Guid);

        BindingEditor.SelectedInput = SelectedInputRow is null
            ? null
            : new InputDescriptorViewModel(SelectedInputRow.InputType, SelectedInputRow.Identifier);
    }

    private void OnDeviceConnected(object? sender, IPhysicalDevice device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Devices.Any(existing => existing.Device.Guid == device.Guid))
                return;

            Devices.Add(new DeviceViewModel(device));
            _liveDevices[device.Guid] = new DeviceLiveInputViewModel(device);

            BindingEditor.RefreshDevices();
            if (SelectedDevice is null)
                SelectedDevice = Devices[^1];

            Dispatcher.UIThread.Post(SyncBindingEditorSelection);
        });
    }

    private void OnDeviceDisconnected(object? sender, IPhysicalDevice device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var deviceViewModel = Devices.FirstOrDefault(existing => existing.Device.Guid == device.Guid);
            var wasSelected = deviceViewModel is not null && ReferenceEquals(SelectedDevice, deviceViewModel);

            if (deviceViewModel is not null)
                Devices.Remove(deviceViewModel);

            if (_liveDevices.TryRemove(device.Guid, out var liveDevice))
                liveDevice.Dispose();

            BindingEditor.RefreshDevices();

            if (wasSelected)
                SelectedDevice = Devices.FirstOrDefault();

            Dispatcher.UIThread.Post(SyncBindingEditorSelection);
        });
    }

    private void OnInputReceived(object? sender, InputEvent inputEvent)
    {
        if (_liveDevices.TryGetValue(inputEvent.DeviceGuid, out var liveDevice))
            liveDevice.ApplyEvent(inputEvent);

        Dispatcher.UIThread.Post(() =>
        {
            var device = Devices.FirstOrDefault(existing => existing.Device.Guid == inputEvent.DeviceGuid);
            if (device is not null)
                device.LastInputLabel = BuildLastInputLabel(inputEvent);

            if (SelectedDevice?.Device.Guid != inputEvent.DeviceGuid)
                return;

            var row = InputRows.FirstOrDefault(existing =>
                existing.InputType == inputEvent.InputType &&
                existing.Identifier == inputEvent.Identifier);

            row?.MarkActive();
        });
    }

    private static string BuildLastInputLabel(InputEvent inputEvent) => inputEvent.InputType switch
    {
        InputType.JoystickAxis => $"Axis {inputEvent.Identifier}: {inputEvent.Value:F2}",
        InputType.JoystickButton => $"Button {inputEvent.Identifier}: {(inputEvent.Value >= 0.5 ? "Pressed" : "Released")}",
        InputType.JoystickHat => $"Hat {inputEvent.Identifier}: {(int)inputEvent.Value}",
        _ => $"{inputEvent.InputType} {inputEvent.Identifier}",
    };

    private void OnProfileChanged(object? sender, Profile? profile)
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.RaisePropertyChanged(nameof(CanEditBinding));
            RebuildInputRows();
        });
    }

    private void DisposeInputRows()
    {
        foreach (var row in InputRows)
            row.Dispose();
    }

    private void DisposeLiveDevices()
    {
        foreach (var liveDevice in _liveDevices.Values)
            liveDevice.Dispose();

        _liveDevices.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _deviceManager.DeviceConnected -= OnDeviceConnected;
        _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;
        _deviceManager.InputReceived -= OnInputReceived;
        _profileState.ProfileChanged -= OnProfileChanged;

        _subscriptions.Dispose();
        DisposeInputRows();
        DisposeLiveDevices();
    }
}
