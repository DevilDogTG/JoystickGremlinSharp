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
    private string _editTargetModeName = string.Empty;
    private string _editMacroKeys = string.Empty;
    private string _editMapToKeyboardKeys = string.Empty;
    private string _editMapToKeyboardBehavior = "Hold";

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
                this.WhenAnyValue(x => x.EditTargetModeName).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditMacroKeys).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditMapToKeyboardKeys).Select(_ => Unit.Default),
                this.WhenAnyValue(x => x.EditMapToKeyboardBehavior).Select(_ => Unit.Default))
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
                break;
            case VJoyHatDescriptor.ActionTag:
                EditVJoyDeviceId = cfg?["vjoyId"]?.GetValue<int>() ?? 1;
                EditVJoyHatIndex = cfg?["hatIndex"]?.GetValue<int>() ?? 1;
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
        _profileState.NotifyProfileModified();
    }

    private static JsonObject? BuildDefaultConfig(string tag) => tag switch
    {
        VJoyAxisDescriptor.ActionTag   => new JsonObject { ["vjoyId"] = 1, ["axisIndex"]   = 1 },
        VJoyButtonDescriptor.ActionTag => new JsonObject { ["vjoyId"] = 1, ["buttonIndex"] = 1 },
        VJoyHatDescriptor.ActionTag    => new JsonObject { ["vjoyId"] = 1, ["hatIndex"]    = 1 },
        ChangeModeActionDescriptor.ActionTag  => new JsonObject { ["targetMode"] = string.Empty },
        MacroActionDescriptor.ActionTag       => new JsonObject { ["keys"] = string.Empty, ["onPress"] = true },
        MapToKeyboardActionDescriptor.ActionTag => new JsonObject { ["keys"] = string.Empty, ["behavior"] = "Hold" },
        _ => null,
    };

    private void RemoveAction()
    {
        if (SelectedBoundAction is null || SelectedBoundAction.IsInherited) return;

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
            },
            VJoyHatDescriptor.ActionTag => new JsonObject
            {
                ["vjoyId"]   = EditVJoyDeviceId,
                ["hatIndex"] = EditVJoyHatIndex,
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

        // Rebuild VM at current index to refresh summary.
        var idx = BoundActions.IndexOf(SelectedBoundAction);
        if (idx >= 0)
        {
            var refreshed = new BoundActionViewModel(model, _actionRegistry);
            BoundActions[idx] = refreshed;
            SelectedBoundAction = refreshed;
        }

        _profileState.NotifyProfileModified();
        _logger.LogTrace("Action config applied for tag {Tag}", model.ActionTag);
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
