using Hermes.Menu;
using Xunit;

namespace Hermes.Tests;

public class AcceleratorTests
{
    [Theory]
    [InlineData("Ctrl+S", true, false, false, false, "S")]
    [InlineData("Ctrl+Shift+S", true, true, false, false, "S")]
    [InlineData("Ctrl+Shift+Alt+S", true, true, true, false, "S")]
    [InlineData("Alt+F4", false, false, true, false, "F4")]
    [InlineData("Shift+Delete", false, true, false, false, "Delete")]
    public void Parse_ValidAccelerator_ReturnsCorrectModifiers(
        string input, bool ctrl, bool shift, bool alt, bool meta, string key)
    {
        var accel = Accelerator.Parse(input);

        Assert.Equal(ctrl, accel.Control);
        Assert.Equal(shift, accel.Shift);
        Assert.Equal(alt, accel.Alt);
        Assert.Equal(meta, accel.Meta);
        Assert.Equal(key, accel.Key);
    }

    [Theory]
    [InlineData("Cmd+S")]
    [InlineData("Command+S")]
    [InlineData("Ctrl+S")]
    [InlineData("Control+S")]
    public void Parse_CmdAndCtrl_BothMapToControl(string input)
    {
        var accel = Accelerator.Parse(input);

        Assert.True(accel.Control);
        Assert.Equal("S", accel.Key);
    }

    [Theory]
    [InlineData("Alt+S")]
    [InlineData("Option+S")]
    public void Parse_AltAndOption_BothMapToAlt(string input)
    {
        var accel = Accelerator.Parse(input);

        Assert.True(accel.Alt);
        Assert.Equal("S", accel.Key);
    }

    [Theory]
    [InlineData("Meta+S")]
    [InlineData("Win+S")]
    [InlineData("Super+S")]
    public void Parse_MetaWinSuper_AllMapToMeta(string input)
    {
        var accel = Accelerator.Parse(input);

        Assert.True(accel.Meta);
        Assert.Equal("S", accel.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Parse_EmptyOrNull_ReturnsDefault(string? input)
    {
        var accel = Accelerator.Parse(input!);

        Assert.False(accel.Control);
        Assert.False(accel.Shift);
        Assert.False(accel.Alt);
        Assert.False(accel.Meta);
        Assert.True(string.IsNullOrEmpty(accel.Key));
    }

    [Fact]
    public void Parse_CaseInsensitive_Works()
    {
        var accel1 = Accelerator.Parse("CTRL+SHIFT+S");
        var accel2 = Accelerator.Parse("ctrl+shift+s");
        var accel3 = Accelerator.Parse("Ctrl+Shift+S");

        Assert.True(accel1.Control);
        Assert.True(accel1.Shift);
        Assert.True(accel2.Control);
        Assert.True(accel2.Shift);
        Assert.True(accel3.Control);
        Assert.True(accel3.Shift);
    }

    [Fact]
    public void ToWindowsString_ReturnsCorrectFormat()
    {
        var accel = Accelerator.Parse("Ctrl+Shift+S");

        var result = accel.ToWindowsString();

        Assert.Equal("Ctrl+Shift+S", result);
    }

    [Fact]
    public void ToMacString_MapsCtrlToCmd()
    {
        var accel = Accelerator.Parse("Ctrl+S");

        var result = accel.ToMacString();

        Assert.Equal("Cmd+S", result);
    }

    [Fact]
    public void ToMacString_MapsAltToOption()
    {
        var accel = Accelerator.Parse("Alt+S");

        var result = accel.ToMacString();

        Assert.Equal("Option+S", result);
    }

    [Fact]
    public void ToMacString_MapsMetaToCtrl()
    {
        var accel = Accelerator.Parse("Meta+S");

        var result = accel.ToMacString();

        Assert.Equal("Ctrl+S", result);
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var success = Accelerator.TryParse("Ctrl+S", out var accel);

        Assert.True(success);
        Assert.True(accel.Control);
        Assert.Equal("S", accel.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void TryParse_EmptyOrNull_ReturnsFalse(string? input)
    {
        var success = Accelerator.TryParse(input, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParse_ModifiersOnly_ReturnsFalse()
    {
        var success = Accelerator.TryParse("Ctrl+Shift+", out _);

        Assert.False(success);
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        Accelerator accel = "Ctrl+S";

        Assert.True(accel.Control);
        Assert.Equal("S", accel.Key);
    }

    [Fact]
    public void Equals_SameAccelerators_ReturnsTrue()
    {
        var accel1 = Accelerator.Parse("Ctrl+S");
        var accel2 = Accelerator.Parse("Ctrl+S");

        Assert.True(accel1.Equals(accel2));
        Assert.True(accel1 == accel2);
    }

    [Fact]
    public void Equals_DifferentCase_ReturnsTrue()
    {
        var accel1 = Accelerator.Parse("Ctrl+S");
        var accel2 = Accelerator.Parse("CTRL+s");

        Assert.True(accel1.Equals(accel2));
    }

    [Fact]
    public void Equals_DifferentAccelerators_ReturnsFalse()
    {
        var accel1 = Accelerator.Parse("Ctrl+S");
        var accel2 = Accelerator.Parse("Ctrl+N");

        Assert.False(accel1.Equals(accel2));
        Assert.True(accel1 != accel2);
    }

    [Fact]
    public void GetHashCode_SameAccelerators_ReturnsSameHash()
    {
        var accel1 = Accelerator.Parse("Ctrl+S");
        var accel2 = Accelerator.Parse("ctrl+s");

        Assert.Equal(accel1.GetHashCode(), accel2.GetHashCode());
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("F12")]
    [InlineData("Delete")]
    [InlineData("Enter")]
    [InlineData("Escape")]
    [InlineData("Tab")]
    [InlineData("Space")]
    public void Parse_SpecialKeys_RecognizedAsKey(string key)
    {
        var accel = Accelerator.Parse(key);

        Assert.Equal(key, accel.Key);
        Assert.False(accel.Control);
        Assert.False(accel.Shift);
        Assert.False(accel.Alt);
        Assert.False(accel.Meta);
    }

    [Fact]
    public void Parse_WithSpaces_TrimsProperly()
    {
        var accel = Accelerator.Parse(" Ctrl + Shift + S ");

        Assert.True(accel.Control);
        Assert.True(accel.Shift);
        Assert.Equal("S", accel.Key);
    }
}
