// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Hermes.Menu;

namespace Hermes.Hotkeys;

/// <summary>
/// Converts an accelerator to the XDG shortcuts format the GlobalShortcuts portal takes, such as CTRL+ALT+k.
/// </summary>
internal static class PortalTrigger
{
    public static string From(Accelerator accelerator)
    {
        var keysym = HotkeyKeyMap.X11KeysymName(accelerator.Key)
            ?? throw new ArgumentException($"'{accelerator.Key}' has no X11 keysym.", nameof(accelerator));

        var builder = new StringBuilder();
        if (accelerator.Control)
            builder.Append("CTRL+");
        if (accelerator.Alt)
            builder.Append("ALT+");
        if (accelerator.Shift)
            builder.Append("SHIFT+");
        if (accelerator.Meta)
            builder.Append("LOGO+");

        return builder.Append(keysym).ToString();
    }
}
