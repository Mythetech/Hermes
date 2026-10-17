// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Hotkeys;
using Hermes.Menu;
using Xunit;

namespace Hermes.Tests.Hotkeys;

public sealed class HotkeyKeyMapTests
{
    public static TheoryData<string> CanonicalKeys()
    {
        var keys = new TheoryData<string>();
        for (var c = 'A'; c <= 'Z'; c++)
            keys.Add(c.ToString());
        for (var c = '0'; c <= '9'; c++)
            keys.Add(c.ToString());
        for (var n = 1; n <= 24; n++)
            keys.Add("F" + n);
        foreach (var name in new[] { "Space", "Enter", "Tab", "Escape", "Backspace", "Delete", "Insert", "Home", "End", "PageUp", "PageDown", "Up", "Down", "Left", "Right" })
            keys.Add(name);
        return keys;
    }

    [Theory]
    [MemberData(nameof(CanonicalKeys))]
    public void EveryKey_NormalizesToItself_AndHasAWindowsAndX11Code(string key)
    {
        Assert.Equal(key, HotkeyKeyMap.Normalize(key));
        Assert.NotNull(HotkeyKeyMap.WindowsVirtualKey(key));
        Assert.NotNull(HotkeyKeyMap.X11KeysymName(key));
    }

    [Theory]
    [MemberData(nameof(CanonicalKeys))]
    public void MacHasEveryKey_ExceptInsertAndF21ToF24(string key)
    {
        var expectedMissing = key is "Insert" or "F21" or "F22" or "F23" or "F24";
        Assert.Equal(expectedMissing, HotkeyKeyMap.MacKeyCode(key) is null);
    }

    [Theory]
    [InlineData("K", (ushort)0x4B, (ushort)0x28, "k")]
    [InlineData("7", (ushort)0x37, (ushort)0x1A, "7")]
    [InlineData("F12", (ushort)0x7B, (ushort)0x6F, "F12")]
    [InlineData("PageUp", (ushort)0x21, (ushort)0x74, "Prior")]
    [InlineData("Enter", (ushort)0x0D, (ushort)0x24, "Return")]
    [InlineData("Backspace", (ushort)0x08, (ushort)0x33, "BackSpace")]
    [InlineData("Delete", (ushort)0x2E, (ushort)0x75, "Delete")]
    [InlineData("Space", (ushort)0x20, (ushort)0x31, "space")]
    public void Codes_MatchEachPlatform(string key, ushort windows, ushort mac, string x11)
    {
        Assert.Equal(windows, HotkeyKeyMap.WindowsVirtualKey(key));
        Assert.Equal(mac, HotkeyKeyMap.MacKeyCode(key));
        Assert.Equal(x11, HotkeyKeyMap.X11KeysymName(key));
    }

    [Theory]
    [InlineData("f5", "F5")]
    [InlineData("k", "K")]
    [InlineData("ESC", "Escape")]
    [InlineData("pgup", "PageUp")]
    [InlineData("Del", "Delete")]
    [InlineData("Ins", "Insert")]
    [InlineData("Back", "Backspace")]
    [InlineData("return", "Enter")]
    public void Normalize_AcceptsAliasesAnyCase(string key, string expected) =>
        Assert.Equal(expected, HotkeyKeyMap.Normalize(key));

    [Theory]
    [InlineData("/")]
    [InlineData("F0")]
    [InlineData("F25")]
    [InlineData("NumLock")]
    [InlineData("")]
    public void Normalize_RejectsOtherKeys(string key) =>
        Assert.Null(HotkeyKeyMap.Normalize(key));

    [Fact]
    public void Modifiers_MapToEachPlatformsMasks()
    {
        var all = new Accelerator { Control = true, Alt = true, Shift = true, Meta = true, Key = "K" };

        Assert.Equal(0xFu, HotkeyKeyMap.WindowsModifiers(all));
        Assert.Equal(0x1B00u, HotkeyKeyMap.MacModifiers(all));
        Assert.Equal(0x4Du, HotkeyKeyMap.X11Modifiers(all));
        Assert.Equal(2u, HotkeyKeyMap.WindowsModifiers(new Accelerator { Control = true, Key = "K" }));
        Assert.Equal(0x100u, HotkeyKeyMap.MacModifiers(new Accelerator { Control = true, Key = "K" }));
        Assert.Equal(0x1000u, HotkeyKeyMap.MacModifiers(new Accelerator { Meta = true, Key = "K" }));
        Assert.Equal(0x40u, HotkeyKeyMap.X11Modifiers(new Accelerator { Meta = true, Key = "K" }));
    }
}
