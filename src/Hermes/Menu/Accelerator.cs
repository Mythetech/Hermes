// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Text;

namespace Hermes.Menu;

/// <summary>
/// Represents a keyboard accelerator (keyboard shortcut) with cross-platform support.
/// Handles conversion between Ctrl (Windows/Linux) and Cmd (macOS).
/// </summary>
public readonly struct Accelerator : IEquatable<Accelerator>
{
    /// <summary>
    /// Control modifier (Ctrl on Windows/Linux, maps to Cmd on macOS).
    /// </summary>
    public bool Control { get; init; }

    /// <summary>
    /// Shift modifier.
    /// </summary>
    public bool Shift { get; init; }

    /// <summary>
    /// Alt modifier (Option on macOS).
    /// </summary>
    public bool Alt { get; init; }

    /// <summary>
    /// Meta modifier (Windows key on Windows, actual Ctrl on macOS).
    /// </summary>
    public bool Meta { get; init; }

    /// <summary>
    /// The key (e.g., "S", "N", "F1", "Delete").
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Parse an accelerator string like "Ctrl+S", "Cmd+Shift+N", "Alt+F4".
    /// </summary>
    /// <param name="accelerator">The accelerator string to parse.</param>
    /// <returns>The parsed Accelerator.</returns>
    public static Accelerator Parse(string accelerator)
    {
        if (string.IsNullOrWhiteSpace(accelerator))
            return default;

        var parts = accelerator.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        bool control = false, shift = false, alt = false, meta = false;
        string key = string.Empty;

        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            switch (lower)
            {
                case "ctrl":
                case "control":
                case "cmd":
                case "command":
                    control = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "alt":
                case "option":
                    alt = true;
                    break;
                case "meta":
                case "win":
                case "super":
                    meta = true;
                    break;
                default:
                    key = part;
                    break;
            }
        }

        return new Accelerator
        {
            Control = control,
            Shift = shift,
            Alt = alt,
            Meta = meta,
            Key = key
        };
    }

    /// <summary>
    /// Try to parse an accelerator string.
    /// </summary>
    /// <param name="accelerator">The accelerator string to parse.</param>
    /// <param name="result">The parsed Accelerator if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string? accelerator, out Accelerator result)
    {
        if (string.IsNullOrWhiteSpace(accelerator))
        {
            result = default;
            return false;
        }

        result = Parse(accelerator);
        return !string.IsNullOrEmpty(result.Key);
    }

    /// <summary>
    /// Convert to platform-native string format.
    /// On macOS, Ctrl becomes Cmd and Alt becomes Option.
    /// </summary>
    public string ToPlatformString()
    {
        if (OperatingSystem.IsMacOS())
            return ToMacString();

        return ToWindowsString();
    }

    /// <summary>
    /// Convert to Windows/Linux format (e.g., "Ctrl+Shift+S").
    /// </summary>
    public string ToWindowsString()
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        var sb = new StringBuilder();

        if (Control)
            sb.Append("Ctrl+");
        if (Shift)
            sb.Append("Shift+");
        if (Alt)
            sb.Append("Alt+");
        if (Meta)
            sb.Append("Win+");

        sb.Append(Key);
        return sb.ToString();
    }

    /// <summary>
    /// Convert to macOS format (e.g., "Cmd+Shift+S").
    /// Ctrl maps to Cmd, Alt maps to Option.
    /// </summary>
    public string ToMacString()
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        var sb = new StringBuilder();

        if (Control)
            sb.Append("Cmd+");
        if (Shift)
            sb.Append("Shift+");
        if (Alt)
            sb.Append("Option+");
        if (Meta)
            sb.Append("Ctrl+");

        sb.Append(Key);
        return sb.ToString();
    }

    /// <summary>
    /// Convert to Linux format (same as Windows).
    /// </summary>
    public string ToLinuxString() => ToWindowsString();

    /// <summary>
    /// Implicit conversion from string.
    /// </summary>
    public static implicit operator Accelerator(string s) => Parse(s);

    /// <summary>
    /// Returns the platform-native string representation.
    /// </summary>
    public override string ToString() => ToPlatformString();

    public bool Equals(Accelerator other)
    {
        return Control == other.Control
            && Shift == other.Shift
            && Alt == other.Alt
            && Meta == other.Meta
            && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is Accelerator other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Control, Shift, Alt, Meta, Key?.ToLowerInvariant());
    }

    public static bool operator ==(Accelerator left, Accelerator right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Accelerator left, Accelerator right)
    {
        return !left.Equals(right);
    }
}
