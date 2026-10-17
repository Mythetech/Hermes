// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Globalization;
using Hermes.Menu;

namespace Hermes.Hotkeys;

/// <summary>
/// The keys global hotkeys support, with each platform's key code and modifier masks.
/// </summary>
internal static class HotkeyKeyMap
{
    private static readonly Dictionary<string, string> s_named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = "Space",
        ["Enter"] = "Enter",
        ["Return"] = "Enter",
        ["Tab"] = "Tab",
        ["Escape"] = "Escape",
        ["Esc"] = "Escape",
        ["Backspace"] = "Backspace",
        ["Back"] = "Backspace",
        ["Delete"] = "Delete",
        ["Del"] = "Delete",
        ["Insert"] = "Insert",
        ["Ins"] = "Insert",
        ["Home"] = "Home",
        ["End"] = "End",
        ["PageUp"] = "PageUp",
        ["PgUp"] = "PageUp",
        ["PageDown"] = "PageDown",
        ["PgDn"] = "PageDown",
        ["Up"] = "Up",
        ["Down"] = "Down",
        ["Left"] = "Left",
        ["Right"] = "Right",
    };

    // Carbon kVK_* codes, which follow key positions rather than letters.
    private static readonly Dictionary<string, ushort> s_macKeyCodes = new()
    {
        ["A"] = 0x00, ["S"] = 0x01, ["D"] = 0x02, ["F"] = 0x03, ["H"] = 0x04, ["G"] = 0x05, ["Z"] = 0x06,
        ["X"] = 0x07, ["C"] = 0x08, ["V"] = 0x09, ["B"] = 0x0B, ["Q"] = 0x0C, ["W"] = 0x0D, ["E"] = 0x0E,
        ["R"] = 0x0F, ["Y"] = 0x10, ["T"] = 0x11, ["1"] = 0x12, ["2"] = 0x13, ["3"] = 0x14, ["4"] = 0x15,
        ["6"] = 0x16, ["5"] = 0x17, ["9"] = 0x19, ["7"] = 0x1A, ["8"] = 0x1C, ["0"] = 0x1D, ["O"] = 0x1F,
        ["U"] = 0x20, ["I"] = 0x22, ["P"] = 0x23, ["L"] = 0x25, ["J"] = 0x26, ["K"] = 0x28, ["N"] = 0x2D,
        ["M"] = 0x2E,
        ["Enter"] = 0x24, ["Tab"] = 0x30, ["Space"] = 0x31, ["Backspace"] = 0x33, ["Escape"] = 0x35,
        ["Delete"] = 0x75, ["Home"] = 0x73, ["End"] = 0x77, ["PageUp"] = 0x74, ["PageDown"] = 0x79,
        ["Left"] = 0x7B, ["Right"] = 0x7C, ["Down"] = 0x7D, ["Up"] = 0x7E,
        ["F1"] = 0x7A, ["F2"] = 0x78, ["F3"] = 0x63, ["F4"] = 0x76, ["F5"] = 0x60, ["F6"] = 0x61,
        ["F7"] = 0x62, ["F8"] = 0x64, ["F9"] = 0x65, ["F10"] = 0x6D, ["F11"] = 0x67, ["F12"] = 0x6F,
        ["F13"] = 0x69, ["F14"] = 0x6B, ["F15"] = 0x71, ["F16"] = 0x6A, ["F17"] = 0x40, ["F18"] = 0x4F,
        ["F19"] = 0x50, ["F20"] = 0x5A,
    };

    /// <summary>The canonical name of a supported key ("K", "7", "F12", "PageUp"), or null.</summary>
    public static string? Normalize(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
            return key.ToUpperInvariant();

        if (s_named.TryGetValue(key, out var named))
            return named;

        return FunctionNumber(key) is { } n ? "F" + n.ToString(CultureInfo.InvariantCulture) : null;
    }

    public static ushort? WindowsVirtualKey(string canonicalKey) => canonicalKey switch
    {
        [var c] when char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) => c,
        "Space" => 0x20,
        "Enter" => 0x0D,
        "Tab" => 0x09,
        "Escape" => 0x1B,
        "Backspace" => 0x08,
        "Delete" => 0x2E,
        "Insert" => 0x2D,
        "Home" => 0x24,
        "End" => 0x23,
        "PageUp" => 0x21,
        "PageDown" => 0x22,
        "Left" => 0x25,
        "Up" => 0x26,
        "Right" => 0x27,
        "Down" => 0x28,
        _ => FunctionNumber(canonicalKey) is { } n ? (ushort)(0x70 + n - 1) : null,
    };

    public static ushort? MacKeyCode(string canonicalKey) =>
        s_macKeyCodes.TryGetValue(canonicalKey, out var code) ? code : null;

    public static string? X11KeysymName(string canonicalKey) => canonicalKey switch
    {
        [var c] when char.IsAsciiLetterUpper(c) => char.ToLowerInvariant(c).ToString(),
        [var c] when char.IsAsciiDigit(c) => canonicalKey,
        "Space" => "space",
        "Enter" => "Return",
        "Tab" => "Tab",
        "Escape" => "Escape",
        "Backspace" => "BackSpace",
        "Delete" => "Delete",
        "Insert" => "Insert",
        "Home" => "Home",
        "End" => "End",
        "PageUp" => "Prior",
        "PageDown" => "Next",
        "Left" => "Left",
        "Up" => "Up",
        "Right" => "Right",
        "Down" => "Down",
        _ => FunctionNumber(canonicalKey) is not null ? canonicalKey : null,
    };

    public static uint WindowsModifiers(Accelerator accelerator) =>
        (accelerator.Alt ? 0x1u : 0) | (accelerator.Control ? 0x2u : 0) | (accelerator.Shift ? 0x4u : 0) | (accelerator.Meta ? 0x8u : 0);

    public static uint MacModifiers(Accelerator accelerator) =>
        (accelerator.Control ? 0x100u : 0) | (accelerator.Shift ? 0x200u : 0) | (accelerator.Alt ? 0x800u : 0) | (accelerator.Meta ? 0x1000u : 0);

    public static uint X11Modifiers(Accelerator accelerator) =>
        (accelerator.Shift ? 0x1u : 0) | (accelerator.Control ? 0x4u : 0) | (accelerator.Alt ? 0x8u : 0) | (accelerator.Meta ? 0x40u : 0);

    private static int? FunctionNumber(string key) =>
        key.Length is >= 2 and <= 3
        && key[0] is 'F' or 'f'
        && int.TryParse(key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
        && n is >= 1 and <= 24
            ? n
            : null;
}
