// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Hotkeys;
using Xunit;

namespace Hermes.Tests.Hotkeys;

public sealed class PortalTriggerTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Shift+F12", "CTRL+ALT+SHIFT+F12")]
    [InlineData("Meta+K", "LOGO+k")]
    [InlineData("Ctrl+PageUp", "CTRL+Prior")]
    [InlineData("Shift+Alt+Space", "ALT+SHIFT+space")]
    [InlineData("Ctrl+7", "CTRL+7")]
    public void From_UsesTheXdgShortcutFormat(string accelerator, string expected) =>
        Assert.Equal(expected, PortalTrigger.From(HotkeyAccelerator.Parse(accelerator, "accelerator")));
}
