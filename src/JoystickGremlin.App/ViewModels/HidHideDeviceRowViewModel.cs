// SPDX-License-Identifier: GPL-3.0-only

using ReactiveUI;

namespace JoystickGremlin.App.ViewModels;

/// <summary>
/// Represents a single physical HID device row in the HidHide device picker.
/// </summary>
public sealed class HidHideDeviceRowViewModel : ReactiveObject
{
    private bool _isHidden;

    /// <summary>
    /// Initializes a new <see cref="HidHideDeviceRowViewModel"/>.
    /// </summary>
    /// <param name="instanceId">The Windows Device Instance ID.</param>
    /// <param name="friendlyName">A human-readable device name.</param>
    /// <param name="isHidden">Initial hidden state; set directly to avoid raising <see cref="HideChanged"/> during construction.</param>
    public HidHideDeviceRowViewModel(string instanceId, string friendlyName, bool isHidden = false)
    {
        InstanceId = instanceId;
        FriendlyName = friendlyName;
        _isHidden = isHidden;  // bypass property setter so HideChanged is not raised on construction
    }

    /// <summary>Gets the Windows Device Instance ID (e.g. <c>HID\VID_054C&amp;PID_05C4\…</c>).</summary>
    public string InstanceId { get; }

    /// <summary>Gets the human-readable device name.</summary>
    public string FriendlyName { get; }

    /// <summary>
    /// Gets or sets whether this device should be hidden from other applications.
    /// Changing this value raises <see cref="HideChanged"/>.
    /// </summary>
    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (_isHidden == value)
                return;

            this.RaiseAndSetIfChanged(ref _isHidden, value);
            HideChanged?.Invoke(this, (InstanceId, value, FriendlyName));
        }
    }

    /// <summary>Gets or sets whether this row represents a device not currently connected.</summary>
    public bool IsStale { get; init; }

    /// <summary>
    /// Raised when the user changes the <see cref="IsHidden"/> state.
    /// Carries (InstanceId, IsHidden, FriendlyName).
    /// </summary>
    public event EventHandler<(string InstanceId, bool IsHidden, string FriendlyName)>? HideChanged;

    /// <summary>Raised when the user requests removal of a stale device entry.</summary>
    public event EventHandler<string>? RemoveStaleRequested;

    /// <summary>Raises <see cref="RemoveStaleRequested"/> for this row's <see cref="InstanceId"/>.</summary>
    public void RequestRemove() => RemoveStaleRequested?.Invoke(this, InstanceId);
}
