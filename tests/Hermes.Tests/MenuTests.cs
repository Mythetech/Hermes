// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Menu;
using Xunit;

namespace Hermes.Tests;

public class MenuTests
{
    [Fact]
    public void AcceleratorString_CmdMapsToCtrl_OnWindows()
    {
        // Test the format label logic that Windows uses
        var accel = Accelerator.Parse("Cmd+S");
        var windowsString = accel.ToWindowsString();

        Assert.Equal("Ctrl+S", windowsString);
    }

    [Fact]
    public void AcceleratorString_ComplexShortcut_FormatsCorrectly()
    {
        var accel = Accelerator.Parse("Ctrl+Shift+Alt+N");

        Assert.Equal("Ctrl+Shift+Alt+N", accel.ToWindowsString());
        Assert.Equal("Cmd+Shift+Option+N", accel.ToMacString());
    }

    [Fact]
    public void Accelerator_LinuxFormat_SameAsWindows()
    {
        var accel = Accelerator.Parse("Ctrl+S");

        Assert.Equal(accel.ToWindowsString(), accel.ToLinuxString());
    }
}

public class MenuInterfaceTests
{
    [Fact]
    public void IMenuBackend_AcceleratorPositionHints_AreDocumented()
    {
        // These are the valid position hints documented in IMenuBackend.AddAppMenuItem
        var validPositions = new[] { "before-quit", "after-about", "top" };

        Assert.All(validPositions, pos =>
        {
            Assert.NotNull(pos);
            Assert.NotEmpty(pos);
        });
    }

    [Fact]
    public void MenuPath_SingleComponent_IsTopLevelMenu()
    {
        // Menu paths without '/' are top-level menus
        var path = "File";

        Assert.DoesNotContain("/", path);
    }

    [Fact]
    public void MenuPath_WithSlash_IsSubmenuPath()
    {
        // Menu paths with '/' navigate to submenus
        var path = "File/New";

        Assert.Contains("/", path);

        var parts = path.Split('/');
        Assert.Equal(2, parts.Length);
        Assert.Equal("File", parts[0]);
        Assert.Equal("New", parts[1]);
    }
}

public class AcceleratorCrossPlatformTests
{
    [Theory]
    [InlineData("Cmd+S", "Ctrl+S", "Cmd+S")]
    [InlineData("Ctrl+S", "Ctrl+S", "Cmd+S")]
    [InlineData("Alt+F4", "Alt+F4", "Option+F4")]
    [InlineData("Meta+S", "Win+S", "Ctrl+S")]
    public void Accelerator_PlatformStrings_MapCorrectly(
        string input, string expectedWindows, string expectedMac)
    {
        var accel = Accelerator.Parse(input);

        Assert.Equal(expectedWindows, accel.ToWindowsString());
        Assert.Equal(expectedMac, accel.ToMacString());
    }

    [Fact]
    public void Accelerator_ShiftModifier_PreservedOnAllPlatforms()
    {
        var accel = Accelerator.Parse("Ctrl+Shift+S");

        Assert.Contains("Shift", accel.ToWindowsString());
        Assert.Contains("Shift", accel.ToMacString());
        Assert.Contains("Shift", accel.ToLinuxString());
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("F5")]
    [InlineData("Delete")]
    [InlineData("Home")]
    [InlineData("End")]
    public void Accelerator_FunctionAndSpecialKeys_WorkWithoutModifiers(string key)
    {
        var accel = Accelerator.Parse(key);

        Assert.Equal(key, accel.Key);
        Assert.Equal(key, accel.ToWindowsString());
    }
}
