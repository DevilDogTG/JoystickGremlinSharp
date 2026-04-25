// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using JoystickGremlin.Core.Actions.Keyboard;
using Microsoft.Extensions.Logging;

namespace JoystickGremlin.Interop.Keyboard;

/// <summary>
/// Windows <c>SendInput</c>-based keyboard simulator.
/// Uses hardware scan codes so that games reading raw scan codes (e.g. DCS World, IL-2) work correctly.
/// </summary>
public sealed class SendInputKeyboardSimulator : IKeyboardSimulator
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp     = 0x0002;
    private const uint KeyEventFScanCode  = 0x0008;
    private const uint KeyEventFExtendedKey = 0x0001;

    private readonly ILogger<SendInputKeyboardSimulator> _logger;

    // Windows scan-code table: key name → (scanCode, isExtended).
    // Extended keys require KEYEVENTF_EXTENDEDKEY to be set.
    private static readonly IReadOnlyDictionary<string, (ushort ScanCode, bool IsExtended)> KeyTable =
        new Dictionary<string, (ushort, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            // Alphanumeric
            { "A", (0x1E, false) }, { "B", (0x30, false) }, { "C", (0x2E, false) },
            { "D", (0x20, false) }, { "E", (0x12, false) }, { "F", (0x21, false) },
            { "G", (0x22, false) }, { "H", (0x23, false) }, { "I", (0x17, false) },
            { "J", (0x24, false) }, { "K", (0x25, false) }, { "L", (0x26, false) },
            { "M", (0x32, false) }, { "N", (0x31, false) }, { "O", (0x18, false) },
            { "P", (0x19, false) }, { "Q", (0x10, false) }, { "R", (0x13, false) },
            { "S", (0x1F, false) }, { "T", (0x14, false) }, { "U", (0x16, false) },
            { "V", (0x2F, false) }, { "W", (0x11, false) }, { "X", (0x2D, false) },
            { "Y", (0x15, false) }, { "Z", (0x2C, false) },

            // Digits (top row)
            { "D0", (0x0B, false) }, { "0", (0x0B, false) },
            { "D1", (0x02, false) }, { "1", (0x02, false) },
            { "D2", (0x03, false) }, { "2", (0x03, false) },
            { "D3", (0x04, false) }, { "3", (0x04, false) },
            { "D4", (0x05, false) }, { "4", (0x05, false) },
            { "D5", (0x06, false) }, { "5", (0x06, false) },
            { "D6", (0x07, false) }, { "6", (0x07, false) },
            { "D7", (0x08, false) }, { "7", (0x08, false) },
            { "D8", (0x09, false) }, { "8", (0x09, false) },
            { "D9", (0x0A, false) }, { "9", (0x0A, false) },

            // Function keys
            { "F1",  (0x3B, false) }, { "F2",  (0x3C, false) },
            { "F3",  (0x3D, false) }, { "F4",  (0x3E, false) },
            { "F5",  (0x3F, false) }, { "F6",  (0x40, false) },
            { "F7",  (0x41, false) }, { "F8",  (0x42, false) },
            { "F9",  (0x43, false) }, { "F10", (0x44, false) },
            { "F11", (0x57, false) }, { "F12", (0x58, false) },
            { "F13", (0x64, false) }, { "F14", (0x65, false) },
            { "F15", (0x66, false) }, { "F16", (0x67, false) },
            { "F17", (0x68, false) }, { "F18", (0x69, false) },
            { "F19", (0x6A, false) }, { "F20", (0x6B, false) },
            { "F21", (0x6C, false) }, { "F22", (0x6D, false) },
            { "F23", (0x6E, false) }, { "F24", (0x76, false) },

            // Modifiers
            { "LShift",   (0x2A, false) }, { "Shift",     (0x2A, false) },
            { "RShift",   (0x36, false) },
            { "LControl", (0x1D, false) }, { "Control",   (0x1D, false) }, { "Ctrl", (0x1D, false) },
            { "RControl", (0x1D, true ) },
            { "LAlt",     (0x38, false) }, { "Alt",       (0x38, false) },
            { "RAlt",     (0x38, true ) },
            { "LWin",     (0x5B, true ) }, { "Win",       (0x5B, true ) },
            { "RWin",     (0x5C, true ) },

            // Common keys
            { "Space",     (0x39, false) },
            { "Return",    (0x1C, false) }, { "Enter", (0x1C, false) },
            { "Back",      (0x0E, false) }, { "Backspace", (0x0E, false) },
            { "Tab",       (0x0F, false) },
            { "Escape",    (0x01, false) }, { "Esc", (0x01, false) },
            { "CapsLock",  (0x3A, false) },
            { "NumLock",   (0x45, false) },
            { "ScrollLock",(0x46, false) },
            { "PrintScreen",(0x37, true ) },
            { "Pause",     (0x45, false) },
            { "Insert",    (0x52, true ) },
            { "Delete",    (0x53, true ) },
            { "Home",      (0x47, true ) },
            { "End",       (0x4F, true ) },
            { "Prior",     (0x49, true ) }, { "PageUp",   (0x49, true ) },
            { "Next",      (0x51, true ) }, { "PageDown", (0x51, true ) },

            // Arrow keys
            { "Left",  (0x4B, true) }, { "Right", (0x4D, true) },
            { "Up",    (0x48, true) }, { "Down",  (0x50, true) },

            // Numpad
            { "NumPad0",       (0x52, false) }, { "NumPad1", (0x4F, false) },
            { "NumPad2",       (0x50, false) }, { "NumPad3", (0x51, false) },
            { "NumPad4",       (0x4B, false) }, { "NumPad5", (0x4C, false) },
            { "NumPad6",       (0x4D, false) }, { "NumPad7", (0x47, false) },
            { "NumPad8",       (0x48, false) }, { "NumPad9", (0x49, false) },
            { "Decimal",       (0x53, false) }, { "NumPadDecimal", (0x53, false) },
            { "Add",           (0x4E, false) }, { "NumPadAdd", (0x4E, false) },
            { "Subtract",      (0x4A, false) }, { "NumPadSubtract", (0x4A, false) },
            { "Multiply",      (0x37, false) }, { "NumPadMultiply", (0x37, false) },
            { "Divide",        (0x35, true ) }, { "NumPadDivide", (0x35, true ) },

            // Punctuation / symbols
            { "OemMinus",     (0x0C, false) }, { "Minus", (0x0C, false) },
            { "OemPlus",      (0x0D, false) }, { "Equal", (0x0D, false) },
            { "OemOpenBrackets", (0x1A, false) },
            { "OemCloseBrackets",(0x1B, false) },
            { "OemSemicolon", (0x27, false) },
            { "OemQuotes",    (0x28, false) },
            { "OemBackslash", (0x2B, false) },
            { "OemComma",     (0x33, false) },
            { "OemPeriod",    (0x34, false) },
            { "OemQuestion",  (0x35, false) },
            { "OemTilde",     (0x29, false) },
        };

    /// <summary>
    /// Initializes a new instance of <see cref="SendInputKeyboardSimulator"/>.
    /// </summary>
    public SendInputKeyboardSimulator(ILogger<SendInputKeyboardSimulator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void KeyDown(string keyName)
    {
        if (!KeyTable.TryGetValue(keyName, out var entry))
        {
            _logger.LogWarning("Unknown key name: {KeyName}", keyName);
            return;
        }
        SendKey(entry.ScanCode, entry.IsExtended, keyUp: false);
    }

    /// <inheritdoc/>
    public void KeyUp(string keyName)
    {
        if (!KeyTable.TryGetValue(keyName, out var entry))
        {
            _logger.LogWarning("Unknown key name: {KeyName}", keyName);
            return;
        }
        SendKey(entry.ScanCode, entry.IsExtended, keyUp: true);
    }

    /// <summary>
    /// Returns all key names known to this simulator (for UI key pickers).
    /// </summary>
    public static IEnumerable<string> GetKeyNames() =>
        KeyTable.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Order();

    private static void SendKey(ushort scanCode, bool extended, bool keyUp)
    {
        var flags = KeyEventFScanCode;
        if (extended) flags |= KeyEventFExtendedKey;
        if (keyUp)    flags |= KeyEventFKeyUp;

        var input = new Input
        {
            Type = InputKeyboard,
            U    = new InputUnion
            {
                Ki = new KeybdInput
                {
                    Wvk      = 0,
                    WScan    = scanCode,
                    DwFlags  = flags,
                    Time     = 0,
                    DwExtraInfo = nuint.Zero,
                },
            },
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    // ─── P/Invoke declarations ────────────────────────────────────────────────

    private static partial class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion U;
    }

    // Force union to 32 bytes to match the native Windows MOUSEINPUT size on x64,
    // making total Input = uint(4) + padding(4) + union(32) = 40 bytes = sizeof(INPUT).
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeybdInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort Wvk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public nuint DwExtraInfo;
    }
}
