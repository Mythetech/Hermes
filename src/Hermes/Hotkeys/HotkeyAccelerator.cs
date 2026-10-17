// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Hermes.Menu;

namespace Hermes.Hotkeys;

/// <summary>
/// Parses and validates global hotkey accelerators in the menu syntax.
/// </summary>
internal static class HotkeyAccelerator
{
    private static readonly HashSet<string> s_modifierTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "ctrl", "control", "cmd", "command", "shift", "alt", "option", "meta", "win", "super",
    };

    // Combinations the operating systems keep for themselves.
    private static readonly HashSet<string> s_reserved = new(StringComparer.Ordinal)
    {
        "Alt+F4", "Ctrl+Alt+Delete", "Meta+L", "Meta+Tab",
    };

    /// <summary>Returns the accelerator with its key in canonical form, or throws for anything a hotkey cannot use.</summary>
    public static Accelerator Parse(string? accelerator, string paramName)
    {
        if (string.IsNullOrWhiteSpace(accelerator))
            throw new ArgumentException("The accelerator is empty.", paramName);

        var tokens = accelerator.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Count(t => !s_modifierTokens.Contains(t)) != 1)
            throw new ArgumentException($"'{accelerator}' must name exactly one key, such as Ctrl+Shift+K.", paramName);

        var parsed = Accelerator.Parse(accelerator);
        var key = HotkeyKeyMap.Normalize(parsed.Key)
            ?? throw new ArgumentException($"'{parsed.Key}' is not a key global hotkeys support.", paramName);

        var result = parsed with { Key = key };
        if (!(result.Control || result.Alt || result.Shift || result.Meta))
            throw new ArgumentException($"'{accelerator}' needs at least one modifier.", paramName);

        if (s_reserved.Contains(ToCanonicalString(result)))
            throw new ArgumentException($"'{accelerator}' is reserved by the operating system.", paramName);

        return result;
    }

    public static string ToCanonicalString(Accelerator accelerator)
    {
        var builder = new StringBuilder();
        if (accelerator.Control)
            builder.Append("Ctrl+");
        if (accelerator.Alt)
            builder.Append("Alt+");
        if (accelerator.Shift)
            builder.Append("Shift+");
        if (accelerator.Meta)
            builder.Append("Meta+");

        return builder.Append(accelerator.Key).ToString();
    }
}
