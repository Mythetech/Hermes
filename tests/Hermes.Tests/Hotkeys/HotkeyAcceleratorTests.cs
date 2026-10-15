// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Hotkeys;
using Xunit;

namespace Hermes.Tests.Hotkeys;

public sealed class HotkeyAcceleratorTests
{
    [Theory]
    [InlineData("Ctrl+Shift+K", "Ctrl+Shift+K")]
    [InlineData("cmd+shift+k", "Ctrl+Shift+K")]
    [InlineData("Shift+Alt+Ctrl+F12", "Ctrl+Alt+Shift+F12")]
    [InlineData("Meta+Space", "Meta+Space")]
    [InlineData("Win+Esc", "Meta+Escape")]
    [InlineData("Ctrl+PgDn", "Ctrl+PageDown")]
    [InlineData("Alt+Return", "Alt+Enter")]
    [InlineData("Ctrl+7", "Ctrl+7")]
    public void Parse_NormalizesToTheCanonicalForm(string accelerator, string expected) =>
        Assert.Equal(expected, HotkeyAccelerator.ToCanonicalString(HotkeyAccelerator.Parse(accelerator, "accelerator")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Shift")]
    public void Parse_RejectsMissingKeys(string? accelerator) =>
        Assert.Throws<ArgumentException>(() => HotkeyAccelerator.Parse(accelerator, "accelerator"));

    [Fact]
    public void Parse_RejectsTwoKeys() =>
        Assert.Throws<ArgumentException>(() => HotkeyAccelerator.Parse("Ctrl+K+J", "accelerator"));

    [Theory]
    [InlineData("K")]
    [InlineData("F12")]
    public void Parse_RejectsAKeyWithoutAModifier(string accelerator) =>
        Assert.Throws<ArgumentException>(() => HotkeyAccelerator.Parse(accelerator, "accelerator"));

    [Theory]
    [InlineData("Ctrl+/")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+NumLock")]
    public void Parse_RejectsUnsupportedKeys(string accelerator) =>
        Assert.Throws<ArgumentException>(() => HotkeyAccelerator.Parse(accelerator, "accelerator"));

    [Theory]
    [InlineData("Alt+F4")]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Ctrl+Alt+Del")]
    [InlineData("Meta+L")]
    [InlineData("Win+Tab")]
    public void Parse_RejectsReservedCombinations(string accelerator) =>
        Assert.Throws<ArgumentException>(() => HotkeyAccelerator.Parse(accelerator, "accelerator"));

    [Fact]
    public void Parse_AllowsAReservedKeyWithOtherModifiers() =>
        Assert.Equal("Ctrl+Alt+F4", HotkeyAccelerator.ToCanonicalString(HotkeyAccelerator.Parse("Ctrl+Alt+F4", "accelerator")));
}
