// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using JoystickGremlin.Core.Actions;
using JoystickGremlin.Core.Actions.ChangeMode;
using JoystickGremlin.Core.Actions.Keyboard;
using JoystickGremlin.Core.Actions.Macro;
using JoystickGremlin.Core.Actions.VJoy;
using JoystickGremlin.Core.Devices;
using JoystickGremlin.Core.Modes;
using JoystickGremlin.Core.Profile;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// ViewModel for the Bindings page.
/// Allows the user to select a device and input, then add/remove/configure action bindings
/// for that input within the currently active mode.
/// </summary>
public sealed class BindingsPageViewModel : ViewModelBase, IDisposable
{
    private readonly IDeviceManager _deviceManager;
    private readonly IActionRegistry _actionRegistry;
    private readonly IProfileState _profileState;
    private readonly IModeManager _modeManager;
    private readonly ILogger<BindingsPageViewModel> _logger;

    // Selection state
    private DeviceViewModel? _selectedDevice;
    private InputDescriptorViewModel? _selectedInput;
    private BoundActionViewModel? _selectedBoundAction;
    private IActionDescriptor? _selectedNewActionType;
    private ModeTreeEntry? _selectedEditModeEntry;

    // Edit form state (reflects current SelectedBoundAction config)
    private int _editVJoyDeviceId = 1;
    private int _editVJoyAxisIndex = 1;
    private int _editVJoyButtonIndex = 1;
    private int _editVJoyHatIndex = 1;
    private int _editButtonsToAxesXAxisIndex = 1;
    private int _editButtonsToAxesYAxisIndex = 2;
    private int _editDirectionalUpButtonId = 1;
    private int _editDirectionalDownButtonId = 2;
    private int _editDirectionalLeftButtonId = 3;
    private int _editDirectionalRightButtonId = 4;
    private string _editTargetModeName = string.Empty;
    private string _editMacroKeys = string.Empty;
    private string _editMapToKeyboardKeys = string.Empty;
    private string _editMapToKeyboardBehavior = "Hold";
    private double _editVJoyButtonThreshold = 0.5;

    /// <summary>
    /// Initializes a new instance of <see cref="BindingsPageViewModel"/>.
    /// </summary>
    public BindingsPageViewModel(
        IDeviceManager deviceManager,
        IActionRegistry actionRegistry,
        IProfileState profileState,
        IModeManager modeManager,
        ILogger<BindingsPageViewModel> logger)
    {
        _deviceManager = deviceManager;
        _actionRegistry = actionRegistry;
        _profileState = profileState;
        _modeManager = modeManager;
        _logger = logger;

        Devices = new ObservableCollection<DeviceViewModel>();
        AvailableInputs = new ObservableCollection<InputDescriptorViewModel>();
        BoundActions = new ObservableCollection<BoundActionViewModel>();
        AvailableActionTypes = new ObservableCollection<IActionDescriptor>();
        AvailableModeNames = new ObservableCollection<string>();
        AvailableEditModeEntries = new ObservableCollection<ModeTreeEntry>();

        var hasInput = this.WhenAnyValue(x => x.SelectedInput).Select(i => i is not null);
        var hasSelection = this.WhenAnyValue(x => x.SelectedBoundAction).Select(a => a is not null);
        var hasNewType = this.WhenAnyValue(x => x.SelectedNewActionType).Select(t => t is not null);
        var canAdd = hasInput.CombineLatest(hasNewType, (i, t) => i && t && HasProfile);
        var hasInherited = this.WhenAnyValue(x => x.SelectedBoundAction).Select(a => a?.IsInherited == true);

        AddActionCommand = ReactiveCommand.Create(AddAction, canAdd);
        RemoveActionCommand = ReactiveCommand.Create(RemoveAction, hasSelection);
        MoveUpCommand = ReactiveCommand.Create(MoveUp, hasSelection);
        MoveDownCommand = ReactiveCommand.Create(MoveDown, hasSelection);
        OverrideInheritedActionCommand = ReactiveCommand.Create(OverrideInheritedAction, hasInherited);

        // Rebuild inputs when device selection changes
        _ = this.WhenAnyValue(x => x.SelectedDevice)
            .Subscribe(OnSelectedDeviceChanged);

        // Rebuild bound-action list when input changes
        _ = this.WhenAnyValue(x => x.SelectedInput)
            .Subscribe(_ => RebuildBoundActions());

        // Rebuild bound-action list when the editing mode changes
        _ = this.WhenAnyValue(x => x.SelectedEditModeEntry)
            .Subscribe(_ => RebuildBoundActions());

        // Populate edit form when selected action changes
        _ = this.WhenAnyValue(x => x.SelectedBoundAction)
            .Subscribe(OnSelectedBoundActionChanged);

        // Auto-apply config changes (debounced) so there is no manual Apply step
        _ = Observable.Merge(
                this.WhenAnyValue(x => x.EditVJoyDeviceId).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditVJoyAxisIndex).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditVJoyButtonIndex).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditVJoyHatIndex).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditButtonsToAxesXAxisIndex).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditButtonsToAxesYAxisIndex).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditDirectionalUpButtonId).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditDirectionalDownButtonId).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditDirectionalLeftButtonId).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditDirectionalRightButtonId).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditTargetModeName).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditMacroKeys).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditMapToKeyboardKeys).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditMapToKeyboardBehavior).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditVJoyButtonThreshold).Select(_ => Unit.Default))
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe(_ => Dispatcher.UIThread.Post(ApplyActionConfig));

        _profileState.ProfileChanged += OnProfileChanged;
        _modeManager.ModeChanged += OnModeChanged;
    }

    // ─── Collections ───────────────────────────────────────────────────────────

    /// <summary>Gets the list of connected physical devices.</summary>
    public ObservableCollection<DeviceViewModel> Devices { get; }

    /// <summary>Gets the available inputs for the selected device.</summary>
    public ObservableCollection<InputDescriptorViewModel> AvailableInputs { get; }

    /// <summary>Gets the bound actions for the selected input in the current mode.</summary>
    public ObservableCollection<BoundActionViewModel> BoundActions { get; }

    /// <summary>Gets the action types available for adding.</summary>
    public ObservableCollection<IActionDescriptor> AvailableActionTypes { get; }

    /// <summary>Gets the mode names available for the change-mode config form.</summary>
    public ObservableCollection<string> AvailableModeNames { get; }

    /// <summary>Gets the mode entries available for editing (DFS tree order, same as toolbar).</summary>
    public ObservableCollection<ModeTreeEntry> AvailableEditModeEntries { get; }

    /// <summary>Gets or sets the mode currently being edited in the bindings panel.</summary>
    public ModeTreeEntry? SelectedEditModeEntry
    {
        get => _selectedEditModeEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedEditModeEntry, value);
    }

    // ─── Selection Properties ───────────────────────────────────────────────────

    /// <summary>Gets or sets the selected device.</summary>
    public DeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    /// <summary>Gets or sets the selected input descriptor.</summary>
    public InputDescriptorViewModel? SelectedInput
    {
        get => _selectedInput;
        set => this.RaiseAndSetIfChanged(ref _selectedInput, value);
    }

    /// <summary>Gets or sets the selected bound action.</summary>
    public BoundActionViewModel? SelectedBoundAction
    {
        get => _selectedBoundAction;
        set => this.RaiseAndSetIfChanged(ref _selectedBoundAction, value);
    }

    /// <summary>Gets or sets the action type to add via <see cref="AddActionCommand"/>.</summary>
    public IActionDescriptor? SelectedNewActionType
    {
        get => _selectedNewActionType;
        set => this.RaiseAndSetIfChanged(ref _selectedNewActionType, value);
    }

    /// <summary>Gets whether a profile is currently loaded.</summary>
    public bool HasProfile => _profileState.CurrentProfile is not null;

    /// <summary>Gets the currently active mode name.</summary>
    public string CurrentModeName => _modeManager.ActiveModeName;

    // ─── Config Form Properties ─────────────────────────────────────────────────

    /// <summary>Gets or sets the vJoy device ID for the selected vJoy action.</summary>
    public int EditVJoyDeviceId
    {
        get => _editVJoyDeviceId;
        set => this.RaiseAndSetIfChanged(ref _editVJoyDeviceId, value);
    }

    /// <summary>Gets or sets the vJoy axis index for the selected vJoy-axis action.</summary>
    public int EditVJoyAxisIndex
    {
        get => _editVJoyAxisIndex;
        set => this.RaiseAndSetIfChanged(ref _editVJoyAxisIndex, value);
    }

    /// <summary>Gets or sets the vJoy button index for the selected vJoy-button action.</summary>
    public int EditVJoyButtonIndex
    {
        get => _editVJoyButtonIndex;
        set => this.RaiseAndSetIfChanged(ref _editVJoyButtonIndex, value);
    }

    /// <summary>Gets or sets the vJoy hat index for the selected vJoy-hat action.</summary>
    public int EditVJoyHatIndex
    {
        get => _editVJoyHatIndex;
        set => this.RaiseAndSetIfChanged(ref _editVJoyHatIndex, value);
    }

    /// <summary>Gets or sets the X axis index for the selected buttons-to-axes action.</summary>
    public int EditButtonsToAxesXAxisIndex
    {
        get => _editButtonsToAxesXAxisIndex;
        set => this.RaiseAndSetIfChanged(ref _editButtonsToAxesXAxisIndex, value);
    }

    /// <summary>Gets or sets the Y axis index for the selected buttons-to-axes action.</summary>
    public int EditButtonsToAxesYAxisIndex
    {
        get => _editButtonsToAxesYAxisIndex;
        set => this.RaiseAndSetIfChanged(ref _editButtonsToAxesYAxisIndex, value);
    }

    /// <summary>Gets or sets the source button ID used for the Up direction.</summary>
    public int EditDirectionalUpButtonId
    {
        get => _editDirectionalUpButtonId;
        set => this.RaiseAndSetIfChanged(ref _editDirectionalUpButtonId, value);
    }

    /// <summary>Gets or sets the source button ID used for the Down direction.</summary>
    public int EditDirectionalDownButtonId
    {
        get => _editDirectionalDownButtonId;
        set => this.RaiseAndSetIfChanged(ref _editDirectionalDownButtonId, value);
    }

    /// <summary>Gets or sets the source button ID used for the Left direction.</summary>
    public int EditDirectionalLeftButtonId
    {
        get => _editDirectionalLeftButtonId;
        set => this.RaiseAndSetIfChanged(ref _editDirectionalLeftButtonId, value);
    }

    /// <summary>Gets or sets the source button ID used for the Right direction.</summary>
    public int EditDirectionalRightButtonId
    {
        get => _editDirectionalRightButtonId;
        set => this.RaiseAndSetIfChanged(ref _editDirectionalRightButtonId, value);
    }

    /// <summary>Gets or sets the target mode name for the change-mode action config form.</summary>
    public string EditTargetModeName
    {
        get => _editTargetModeName;
        set => this.RaiseAndSetIfChanged(ref _editTargetModeName, value);
    }

    /// <summary>Gets or sets the comma-separated key names for the macro action config form.</summary>
    public string EditMacroKeys
    {
        get => _editMacroKeys;
        set => this.RaiseAndSetIfChanged(ref _editMacroKeys, value);
    }

    /// <summary>Gets or sets the comma-separated key names for the map-to-keyboard action config form.</summary>
    public string EditMapToKeyboardKeys
    {
        get => _editMapToKeyboardKeys;
        set => this.RaiseAndSetIfChanged(ref _editMapToKeyboardKeys, value);
    }

    /// <summary>Gets or sets the behavior string for the map-to-keyboard action (Hold/Toggle/PressOnly/ReleaseOnly).</summary>
    public string EditMapToKeyboardBehavior
    {
        get => _editMapToKeyboardBehavior;
        set => this.RaiseAndSetIfChanged(ref _editMapToKeyboardBehavior, value);
    }

    /// <summary>Gets the available behavior options for the map-to-keyboard action.</summary>
    public static IReadOnlyList<string> MapToKeyboardBehaviors { get; } =
        ["Hold", "Toggle", "PressOnly", "ReleaseOnly"];

    /// <summary>Gets or sets the axis-to-button press threshold (0–1) for the vJoy-button action.</summary>
    public double EditVJoyButtonThreshold
    {
        get => _editVJoyButtonThreshold;
        set => this.RaiseAndSetIfChanged(ref _editVJoyButtonThreshold, value);
    }

    // ─── Visibility Helpers ─────────────────────────────────────────────────────

    /// <summary>Gets whether the vJoy axis config section should be shown.</summary>
    public bool ShowVJoyAxisConfig => SelectedBoundAction?.IsVJoyAxis ?? false;

    /// <summary>Gets whether the vJoy button config section should be shown.</summary>
    public bool ShowVJoyButtonConfig => SelectedBoundAction?.IsVJoyButton ?? false;

    /// <summary>Gets whether the vJoy hat config section should be shown.</summary>
    public bool ShowVJoyHatConfig => SelectedBoundAction?.IsVJoyHat ?? false;

    /// <summary>Gets whether the change-mode config section should be shown.</summary>
    public bool ShowChangeModeConfig => SelectedBoundAction?.IsChangeMode ?? false;

    /// <summary>Gets whether the macro config section should be shown.</summary>
    public bool ShowMacroConfig => SelectedBoundAction?.IsMacro ?? false;

    /// <summary>Gets whether the map-to-keyboard config section should be shown.</summary>
    public bool ShowMapToKeyboardConfig => SelectedBoundAction?.IsMapToKeyboard ?? false;

    /// <summary>Gets whether the buttons-to-hat config section should be shown.</summary>
    public bool ShowButtonsToHatConfig => SelectedBoundAction?.IsButtonsToHat ?? false;

    /// <summary>Gets whether the buttons-to-axes config section should be shown.</summary>
    public bool ShowButtonsToAxesConfig => SelectedBoundAction?.IsButtonsToAxes ?? false;

    /// <summary>Gets whether any config section is visible (i.e. a non-inherited action is selected).</summary>
    public bool ShowConfigPanel => SelectedBoundAction is not null && !SelectedBoundAction.IsInherited;

    /// <summary>Gets whether the "Override in this mode" panel should be shown.</summary>
    public bool ShowInheritedPanel => SelectedBoundAction?.IsInherited == true;

    // ─── Commands ───────────────────────────────────────────────────────────────

    /// <summary>Gets the command that adds a new action to the selected input's binding.</summary>
    public ReactiveCommand<Unit, Unit> AddActionCommand { get; }

    /// <summary>Gets the command that removes the selected bound action.</summary>
    public ReactiveCommand<Unit, Unit> RemoveActionCommand { get; }

    /// <summary>Gets the command that moves the selected action up in the list.</summary>
    public ReactiveCommand<Unit, Unit> MoveUpCommand { get; }

    /// <summary>Gets the command that moves the selected action down in the list.</summary>
    public ReactiveCommand<Unit, Unit> MoveDownCommand { get; }

    /// <summary>
    /// Gets the command that copies the selected inherited action into the editing mode,
    /// creating a local override.
    /// </summary>
    public ReactiveCommand<Unit, Unit> OverrideInheritedActionCommand { get; }

    // ─── Public Methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the device list from <see cref="IDeviceManager.Devices"/> and repopulates
    /// the action-type registry. Call after devices are initialized.
    /// </summary>
    public void RefreshDevices()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Devices.Clear();
            foreach (var d in _deviceManager.Devices)
                Devices.Add(new DeviceViewModel(d));

            SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
        });

        RefreshActionTypes();
        RebuildEditModeEntries();
    }

    // ─── Private Helpers ────────────────────────────────────────────────────────

    private void RefreshActionTypes()
    {
        AvailableActionTypes.Clear();
        foreach (var descriptor in _actionRegistry.GetAll())
            AvailableActionTypes.Add(descriptor);
        SelectedNewActionType = AvailableActionTypes.Count > 0 ? AvailableActionTypes[0] : null;
    }

    private void RebuildEditModeEntries()
    {
        var profile = _profileState.CurrentProfile;
        var prevName = SelectedEditModeEntry?.Name;

        AvailableEditModeEntries.Clear();
        AvailableModeNames.Clear();

        if (profile is null) return;

        foreach (var entry in ModeTreeHelper.BuildEntries(profile.Modes))
            AvailableEditModeEntries.Add(entry);

        foreach (var mode in profile.Modes)
            AvailableModeNames.Add(mode.Name);

        // Restore previous selection or default to first entry
        SelectedEditModeEntry = AvailableEditModeEntries.FirstOrDefault(e => e.Name == prevName)
                               ?? AvailableEditModeEntries.FirstOrDefault();
    }

    private void OnSelectedDeviceChanged(DeviceViewModel? device)
    {
        AvailableInputs.Clear();
        SelectedInput = null;
        if (device?.Device is null) return;

        for (var i = 1; i <= device.Device.AxisCount; i++)
            AvailableInputs.Add(new InputDescriptorViewModel(InputType.JoystickAxis, i));
        for (var i = 1; i <= device.Device.ButtonCount; i++)
            AvailableInputs.Add(new InputDescriptorViewModel(InputType.JoystickButton, i));
        for (var i = 1; i <= device.Device.HatCount; i++)
            AvailableInputs.Add(new InputDescriptorViewModel(InputType.JoystickHat, i));

        SelectedInput = AvailableInputs.Count > 0 ? AvailableInputs[0] : null;
    }

    private void RebuildBoundActions()
    {
        BoundActions.Clear();
        SelectedBoundAction = null;

        var profile = _profileState.CurrentProfile;
        var editModeName = SelectedEditModeEntry?.Name;
        if (profile is null || editModeName is null || SelectedDevice is null || SelectedInput is null)
            return;

        var deviceGuid = SelectedDevice.Device.Guid;
        var inputType  = SelectedInput.InputType;
        var identifier = SelectedInput.Identifier;

        // Own bindings — defined directly in the editing mode.
        var ownMode = profile.Modes.FirstOrDefault(m => m.Name == editModeName);
        var ownBinding = ownMode?.Bindings.FirstOrDefault(b =>
            b.DeviceGuid == deviceGuid &&
            b.InputType  == inputType  &&
            b.Identifier == identifier);

        if (ownBinding is not null)
        {
            foreach (var ba in ownBinding.Actions)
                BoundActions.Add(new BoundActionViewModel(ba, _actionRegistry));
        }

        // Inherited bindings — first ancestor in the chain that defines this input.
        var chain = _modeManager.GetInheritanceChain(editModeName, profile);
        for (var i = 1; i < chain.Count; i++)
        {
            var ancestorMode = profile.Modes.FirstOrDefault(m => m.Name == chain[i]);
            if (ancestorMode is null) continue;

            var ancestorBinding = ancestorMode.Bindings.FirstOrDefault(b =>
                b.DeviceGuid == deviceGuid &&
                b.InputType  == inputType  &&
                b.Identifier == identifier);

            if (ancestorBinding is null) continue;

            foreach (var ba in ancestorBinding.Actions)
                BoundActions.Add(new BoundActionViewModel(ba, _actionRegistry, chain[i]));

            break; // Only the first ancestor wins (matches runtime first-match behavior).
        }

        // Default selection: first own action; fall back to first inherited.
        SelectedBoundAction = BoundActions.FirstOrDefault(vm => !vm.IsInherited)
                           ?? BoundActions.FirstOrDefault();
    }

    private void OnSelectedBoundActionChanged(BoundActionViewModel? vm)
    {
        this.RaisePropertyChanged(nameof(ShowVJoyAxisConfig));
        this.RaisePropertyChanged(nameof(ShowVJoyButtonConfig));
        this.RaisePropertyChanged(nameof(ShowVJoyHatConfig));
        this.RaisePropertyChanged(nameof(ShowChangeModeConfig));
        this.RaisePropertyChanged(nameof(ShowMacroConfig));
        this.RaisePropertyChanged(nameof(ShowMapToKeyboardConfig));
        this.RaisePropertyChanged(nameof(ShowButtonsToHatConfig));
        this.RaisePropertyChanged(nameof(ShowButtonsToAxesConfig));
        this.RaisePropertyChanged(nameof(ShowConfigPanel));
        this.RaisePropertyChanged(nameof(ShowInheritedPanel));

        if (vm is null) return;

        var cfg = vm.Model.Configuration;
        switch (vm.ActionTag)
        {
            case VJoyAxisDescriptor.ActionTag:
                EditVJoyDeviceId   = cfg?["vjoyId"]?.GetValue<int>() ?? 1;
                EditVJoyAxisIndex  = cfg?["axisIndex"]?.GetValue<int>() ?? 1;
                break;
            case VJoyButtonDescriptor.ActionTag:
                EditVJoyDeviceId     = cfg?["vjoyId"]?.GetValue<int>() ?? 1;
                EditVJoyButtonIndex  = cfg?["buttonIndex"]?.GetValue<int>() ?? 1;
                EditVJoyButtonThreshold = cfg?["threshold"]?.GetValue<double>() ?? 0.5;
                break;
            case VJoyHatDescriptor.ActionTag:
                EditVJoyDeviceId = cfg?["vjoyId"]?.GetValue<int>() ?? 1;
                EditVJoyHatIndex = cfg?["hatIndex"]?.GetValue<int>() ?? 1;
                break;
            case ButtonsToHatDescriptor.ActionTag:
                EditVJoyDeviceId = cfg?["vjoyId"]?.GetValue<int>() ?? 1;
                EditVJoyHatIndex = cfg?["hatIndex"]?.GetValue<int>() ?? 1;
                EditDirectionalUpButtonId = cfg?["upButtonId"]?.GetValue<int>() ?? 1;
                EditDirectionalDownButtonId = cfg?["downButtonId"]?.GetValue<int>() ?? 2;
                EditDirectionalLeftButtonId = cfg?["leftButtonId"]?.GetValue<int>() ?? 3;
                EditDirectionalRightButtonId = cfg?["rightButtonId"]?.GetValue<int>() ?? 4;
                break;
            case ButtonsToAxesDescriptor.ActionTag:
                EditVJoyDeviceId = cfg?["vjoyId"]?.GetValue<int>() ?? 1;
                EditButtonsToAxesXAxisIndex = cfg?["xAxisIndex"]?.GetValue<int>() ?? 1;
                EditButtonsToAxesYAxisIndex = cfg?["yAxisIndex"]?.GetValue<int>() ?? 2;
                EditDirectionalUpButtonId = cfg?["upButtonId"]?.GetValue<int>() ?? 1;
                EditDirectionalDownButtonId = cfg?["downButtonId"]?.GetValue<int>() ?? 2;
                EditDirectionalLeftButtonId = cfg?["leftButtonId"]?.GetValue<int>() ?? 3;
                EditDirectionalRightButtonId = cfg?["rightButtonId"]?.GetValue<int>() ?? 4;
                break;
            case ChangeModeActionDescriptor.ActionTag:
                EditTargetModeName = cfg?["targetMode"]?.GetValue<string>() ?? string.Empty;
                break;
            case MacroActionDescriptor.ActionTag:
                EditMacroKeys = cfg?["keys"]?.GetValue<string>() ?? string.Empty;
                break;
            case MapToKeyboardActionDescriptor.ActionTag:
                EditMapToKeyboardKeys     = cfg?["keys"]?.GetValue<string>() ?? string.Empty;
                EditMapToKeyboardBehavior = cfg?["behavior"]?.GetValue<string>() ?? "Hold";
                break;
        }
    }

    private void AddAction()
    {
        if (SelectedNewActionType is null || SelectedInput is null) return;
        if (SelectedEditModeEntry is null) return;
        if (IsMultiButtonAction(SelectedNewActionType.Tag) && SelectedInput.InputType != InputType.JoystickButton)
        {
            _logger.LogWarning(
                "Cannot add action {ActionTag} to non-button input type {InputType}",
                SelectedNewActionType.Tag,
                SelectedInput.InputType);
            return;
        }

        var binding = FindOrCreateBinding(create: true);
        if (binding is null) return;

        var newAction = new BoundAction
        {
            ActionTag = SelectedNewActionType.Tag,
            Configuration = BuildDefaultConfig(SelectedNewActionType.Tag),
        };
        binding.Actions.Add(newAction);

        var vm = new BoundActionViewModel(newAction, _actionRegistry);

        // Insert before any inherited entries to keep own actions at the top.
        var insertIdx = BoundActions.Count(bvm => !bvm.IsInherited);
        BoundActions.Insert(insertIdx, vm);
        SelectedBoundAction = vm;
        SyncMultiButtonBindings(newAction);
        RebuildBoundActions();
        SelectedBoundAction = BoundActions.FirstOrDefault(bvm => !bvm.IsInherited && ReferenceEquals(bvm.Model, newAction))
                           ?? BoundActions.FirstOrDefault(bvm => !bvm.IsInherited && GetMappingId(bvm.Model.Configuration) == GetMappingId(newAction.Configuration))
                           ?? BoundActions.FirstOrDefault(bvm => !bvm.IsInherited);
        _profileState.NotifyProfileModified();
    }

    private JsonObject? BuildDefaultConfig(string tag)
    {
        var selectedButtonId = SelectedInput?.Identifier ?? 1;

        return tag switch
        {
            VJoyAxisDescriptor.ActionTag   => new JsonObject { ["vjoyId"] = 1, ["axisIndex"]   = 1 },
            VJoyButtonDescriptor.ActionTag => new JsonObject { ["vjoyId"] = 1, ["buttonIndex"] = 1 },
            VJoyHatDescriptor.ActionTag    => new JsonObject { ["vjoyId"] = 1, ["hatIndex"]    = 1 },
            ButtonsToHatDescriptor.ActionTag => new JsonObject
            {
                ["mappingId"] = Guid.NewGuid().ToString("N"),
                ["vjoyId"] = 1,
                ["hatIndex"] = 1,
                ["upButtonId"] = selectedButtonId,
                ["downButtonId"] = selectedButtonId,
                ["leftButtonId"] = selectedButtonId,
                ["rightButtonId"] = selectedButtonId,
            },
            ButtonsToAxesDescriptor.ActionTag => new JsonObject
            {
                ["mappingId"] = Guid.NewGuid().ToString("N"),
                ["vjoyId"] = 1,
                ["xAxisIndex"] = 1,
                ["yAxisIndex"] = 2,
                ["upButtonId"] = selectedButtonId,
                ["downButtonId"] = selectedButtonId,
                ["leftButtonId"] = selectedButtonId,
                ["rightButtonId"] = selectedButtonId,
            },
            ChangeModeActionDescriptor.ActionTag  => new JsonObject { ["targetMode"] = string.Empty },
            MacroActionDescriptor.ActionTag       => new JsonObject { ["keys"] = string.Empty, ["onPress"] = true },
            MapToKeyboardActionDescriptor.ActionTag => new JsonObject { ["keys"] = string.Empty, ["behavior"] = "Hold" },
            _ => null,
        };
    }

    private void RemoveAction()
    {
        if (SelectedBoundAction is null || SelectedBoundAction.IsInherited) return;

        if (IsMultiButtonAction(SelectedBoundAction.ActionTag))
        {
            RemoveSynchronizedMultiButtonActions(SelectedBoundAction.Model);
            RebuildBoundActions();
            _profileState.NotifyProfileModified();
            return;
        }

        var binding = FindOrCreateBinding(create: false);
        if (binding is null) return;

        binding.Actions.Remove(SelectedBoundAction.Model);
        BoundActions.Remove(SelectedBoundAction);
        SelectedBoundAction = BoundActions.FirstOrDefault(vm => !vm.IsInherited)
                           ?? BoundActions.FirstOrDefault();
        _profileState.NotifyProfileModified();
    }

    private void MoveUp()
    {
        if (SelectedBoundAction is null || SelectedBoundAction.IsInherited) return;
        var idx = BoundActions.IndexOf(SelectedBoundAction);
        if (idx <= 0) return;

        var binding = FindOrCreateBinding(create: false);
        if (binding is null) return;

        BoundActions.Move(idx, idx - 1);
        var tmp = binding.Actions[idx];
        binding.Actions[idx] = binding.Actions[idx - 1];
        binding.Actions[idx - 1] = tmp;
        _profileState.NotifyProfileModified();
    }

    private void MoveDown()
    {
        if (SelectedBoundAction is null || SelectedBoundAction.IsInherited) return;
        var idx = BoundActions.IndexOf(SelectedBoundAction);
        // Can't move down into inherited territory.
        var maxIdx = BoundActions.Count(vm => !vm.IsInherited) - 1;
        if (idx < 0 || idx >= maxIdx) return;

        var binding = FindOrCreateBinding(create: false);
        if (binding is null) return;

        BoundActions.Move(idx, idx + 1);
        var tmp = binding.Actions[idx];
        binding.Actions[idx] = binding.Actions[idx + 1];
        binding.Actions[idx + 1] = tmp;
        _profileState.NotifyProfileModified();
    }

    private void ApplyActionConfig()
    {
        if (SelectedBoundAction is null) return;
        if (SelectedBoundAction.IsInherited) return; // Inherited actions are read-only.

        var model = SelectedBoundAction.Model;
        model.Configuration = model.ActionTag switch
        {
            VJoyAxisDescriptor.ActionTag => new JsonObject
            {
                ["vjoyId"]    = EditVJoyDeviceId,
                ["axisIndex"] = EditVJoyAxisIndex,
            },
            VJoyButtonDescriptor.ActionTag => new JsonObject
            {
                ["vjoyId"]      = EditVJoyDeviceId,
                ["buttonIndex"] = EditVJoyButtonIndex,
                ["threshold"]   = EditVJoyButtonThreshold,
            },
            VJoyHatDescriptor.ActionTag => new JsonObject
            {
                ["vjoyId"]   = EditVJoyDeviceId,
                ["hatIndex"] = EditVJoyHatIndex,
            },
            ButtonsToHatDescriptor.ActionTag => new JsonObject
            {
                ["mappingId"] = GetOrCreateMappingId(model.Configuration),
                ["vjoyId"] = EditVJoyDeviceId,
                ["hatIndex"] = EditVJoyHatIndex,
                ["upButtonId"] = EditDirectionalUpButtonId,
                ["downButtonId"] = EditDirectionalDownButtonId,
                ["leftButtonId"] = EditDirectionalLeftButtonId,
                ["rightButtonId"] = EditDirectionalRightButtonId,
            },
            ButtonsToAxesDescriptor.ActionTag => new JsonObject
            {
                ["mappingId"] = GetOrCreateMappingId(model.Configuration),
                ["vjoyId"] = EditVJoyDeviceId,
                ["xAxisIndex"] = EditButtonsToAxesXAxisIndex,
                ["yAxisIndex"] = EditButtonsToAxesYAxisIndex,
                ["upButtonId"] = EditDirectionalUpButtonId,
                ["downButtonId"] = EditDirectionalDownButtonId,
                ["leftButtonId"] = EditDirectionalLeftButtonId,
                ["rightButtonId"] = EditDirectionalRightButtonId,
            },
            ChangeModeActionDescriptor.ActionTag => new JsonObject
            {
                ["targetMode"] = EditTargetModeName,
            },
            MacroActionDescriptor.ActionTag => new JsonObject
            {
                ["keys"]    = EditMacroKeys,
                ["onPress"] = true,
            },
            MapToKeyboardActionDescriptor.ActionTag => new JsonObject
            {
                ["keys"]     = EditMapToKeyboardKeys,
                ["behavior"] = EditMapToKeyboardBehavior,
            },
            _ => model.Configuration,
        };

        SyncMultiButtonBindings(model);

        if (IsMultiButtonAction(model.ActionTag))
        {
            var mappingId = GetMappingId(model.Configuration);
            RebuildBoundActions();
            SelectedBoundAction = mappingId is null
                ? BoundActions.FirstOrDefault(vm => !vm.IsInherited)
                : BoundActions.FirstOrDefault(vm =>
                    !vm.IsInherited &&
                    vm.ActionTag == model.ActionTag &&
                    GetMappingId(vm.Model.Configuration) == mappingId);
        }
        else
        {
            // Rebuild VM at current index to refresh summary.
            var idx = BoundActions.IndexOf(SelectedBoundAction);
            if (idx >= 0)
            {
                var refreshed = new BoundActionViewModel(model, _actionRegistry);
                BoundActions[idx] = refreshed;
                SelectedBoundAction = refreshed;
            }
        }

        _profileState.NotifyProfileModified();
        _logger.LogTrace("Action config applied for tag {Tag}", model.ActionTag);
    }

    private void SyncMultiButtonBindings(BoundAction model)
    {
        if (!IsMultiButtonAction(model.ActionTag)) return;

        var profile = _profileState.CurrentProfile;
        if (profile is null || SelectedDevice is null || SelectedEditModeEntry is null) return;

        var mappingId = GetOrCreateMappingId(model.Configuration);
        var mode = profile.Modes.FirstOrDefault(m => m.Name == SelectedEditModeEntry.Name);
        if (mode is null) return;

        var referencedButtonIds = GetReferencedButtonIds(model.Configuration);
        var deviceGuid = SelectedDevice.Device.Guid;

        foreach (var binding in mode.Bindings
                     .Where(b => b.DeviceGuid == deviceGuid && b.InputType == InputType.JoystickButton))
        {
            var peerAction = binding.Actions.FirstOrDefault(a =>
                a.ActionTag == model.ActionTag &&
                GetMappingId(a.Configuration) == mappingId);

            if (peerAction is null) continue;

            if (!referencedButtonIds.Contains(binding.Identifier))
            {
                binding.Actions.Remove(peerAction);
                continue;
            }

            if (!ReferenceEquals(peerAction, model))
            {
                peerAction.Configuration = (JsonObject?)model.Configuration?.DeepClone();
            }
        }

        foreach (var buttonId in referencedButtonIds)
        {
            var binding = FindOrCreateButtonBinding(mode, deviceGuid, buttonId, create: true);
            if (binding is null) continue;

            var peerAction = binding.Actions.FirstOrDefault(a =>
                a.ActionTag == model.ActionTag &&
                GetMappingId(a.Configuration) == mappingId);

            if (peerAction is null)
            {
                if (ReferenceEquals(model, binding.Actions.FirstOrDefault(a => ReferenceEquals(a, model))))
                {
                    continue;
                }

                binding.Actions.Add(new BoundAction
                {
                    ActionTag = model.ActionTag,
                    Configuration = (JsonObject?)model.Configuration?.DeepClone(),
                });
            }
            else if (!ReferenceEquals(peerAction, model))
            {
                peerAction.Configuration = (JsonObject?)model.Configuration?.DeepClone();
            }
        }
    }

    private void RemoveSynchronizedMultiButtonActions(BoundAction model)
    {
        var profile = _profileState.CurrentProfile;
        if (profile is null || SelectedDevice is null || SelectedEditModeEntry is null)
        {
            RemoveCurrentActionOnly(model);
            return;
        }

        var mappingId = GetMappingId(model.Configuration);
        if (mappingId is null)
        {
            RemoveCurrentActionOnly(model);
            return;
        }

        var mode = profile.Modes.FirstOrDefault(m => m.Name == SelectedEditModeEntry.Name);
        if (mode is null)
        {
            RemoveCurrentActionOnly(model);
            return;
        }

        var deviceGuid = SelectedDevice.Device.Guid;
        foreach (var binding in mode.Bindings
                     .Where(b => b.DeviceGuid == deviceGuid && b.InputType == InputType.JoystickButton))
        {
            binding.Actions.RemoveAll(action =>
                action.ActionTag == model.ActionTag &&
                GetMappingId(action.Configuration) == mappingId);
        }
    }

    private void RemoveCurrentActionOnly(BoundAction model)
    {
        var binding = FindOrCreateBinding(create: false);
        binding?.Actions.Remove(model);
    }

    private static bool IsMultiButtonAction(string actionTag) =>
        actionTag is ButtonsToHatDescriptor.ActionTag or ButtonsToAxesDescriptor.ActionTag;

    private static string GetOrCreateMappingId(JsonObject? configuration)
    {
        if (configuration?["mappingId"]?.GetValue<string>() is { Length: > 0 } mappingId)
        {
            return mappingId;
        }

        mappingId = Guid.NewGuid().ToString("N");
        configuration?["mappingId"] = mappingId;
        return mappingId;
    }

    private static string? GetMappingId(JsonObject? configuration) =>
        configuration?["mappingId"]?.GetValue<string>();

    private static HashSet<int> GetReferencedButtonIds(JsonObject? configuration)
    {
        HashSet<int> buttonIds = [];

        AddIfPositive(buttonIds, configuration?["upButtonId"]?.GetValue<int>() ?? 0);
        AddIfPositive(buttonIds, configuration?["downButtonId"]?.GetValue<int>() ?? 0);
        AddIfPositive(buttonIds, configuration?["leftButtonId"]?.GetValue<int>() ?? 0);
        AddIfPositive(buttonIds, configuration?["rightButtonId"]?.GetValue<int>() ?? 0);

        return buttonIds;
    }

    private static void AddIfPositive(ISet<int> target, int value)
    {
        if (value > 0)
        {
            target.Add(value);
        }
    }

    private static InputBinding? FindOrCreateButtonBinding(Mode mode, Guid deviceGuid, int buttonId, bool create)
    {
        var binding = mode.Bindings.FirstOrDefault(
            b => b.DeviceGuid == deviceGuid &&
                 b.InputType == InputType.JoystickButton &&
                 b.Identifier == buttonId);

        if (binding is null && create)
        {
            binding = new InputBinding
            {
                DeviceGuid = deviceGuid,
                InputType = InputType.JoystickButton,
                Identifier = buttonId,
            };
            mode.Bindings.Add(binding);
        }

        return binding;
    }

    private void OverrideInheritedAction()
    {
        if (SelectedBoundAction is null || !SelectedBoundAction.IsInherited) return;

        var binding = FindOrCreateBinding(create: true);
        if (binding is null) return;

        var clonedConfig = (JsonObject?)SelectedBoundAction.Model.Configuration?.DeepClone();
        var newAction = new BoundAction
        {
            ActionTag = SelectedBoundAction.Model.ActionTag,
            Configuration = clonedConfig,
        };
        binding.Actions.Add(newAction);

        RebuildBoundActions();
        SelectedBoundAction = BoundActions.FirstOrDefault(vm => !vm.IsInherited && vm.ActionTag == newAction.ActionTag)
                           ?? BoundActions.FirstOrDefault(vm => !vm.IsInherited);
        _profileState.NotifyProfileModified();
    }

    /// <summary>
    /// Finds the <see cref="InputBinding"/> for the selected device+input in the editing mode.
    /// If <paramref name="create"/> is true, creates the binding (and mode if missing) when absent.
    /// </summary>
    private InputBinding? FindOrCreateBinding(bool create)
    {
        var profile = _profileState.CurrentProfile;
        if (profile is null || SelectedDevice is null || SelectedInput is null || SelectedEditModeEntry is null)
            return null;

        var modeName = SelectedEditModeEntry.Name;
        var mode = profile.Modes.FirstOrDefault(m => m.Name == modeName);

        if (mode is null)
        {
            if (!create) return null;
            mode = new Mode { Name = modeName };
            profile.Modes.Add(mode);
        }

        var deviceGuid = SelectedDevice.Device.Guid;
        var inputType  = SelectedInput.InputType;
        var identifier = SelectedInput.Identifier;

        var binding = mode.Bindings.FirstOrDefault(
            b => b.DeviceGuid == deviceGuid &&
                 b.InputType  == inputType   &&
                 b.Identifier == identifier);

        if (binding is null)
        {
            if (!create) return null;
            binding = new InputBinding
            {
                DeviceGuid = deviceGuid,
                InputType  = inputType,
                Identifier = identifier,
            };
            mode.Bindings.Add(binding);
        }

        return binding;
    }

    private void OnProfileChanged(object? sender, Profile? profile)
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.RaisePropertyChanged(nameof(HasProfile));
            RebuildEditModeEntries();
            RebuildBoundActions();
        });
    }

    private void OnModeChanged(object? sender, string modeName)
    {
        Dispatcher.UIThread.Post(() => this.RaisePropertyChanged(nameof(CurrentModeName)));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _profileState.ProfileChanged -= OnProfileChanged;
        _modeManager.ModeChanged -= OnModeChanged;
    }
}
