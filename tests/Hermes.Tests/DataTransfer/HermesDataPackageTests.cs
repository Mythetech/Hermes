// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class HermesDataPackageTests
{
    [Fact]
    public void NewPackage_IsEmpty_AndCustomIsEmptyNotNull()
    {
        var package = new HermesDataPackage();

        Assert.True(package.IsEmpty);
        Assert.Equal(HermesDataFormats.None, package.Formats);
        Assert.NotNull(package.Custom);
        Assert.Empty(package.Custom);
    }

    [Fact]
    public void Formats_ReportsEveryStandardFormatPresent()
    {
        var package = new HermesDataPackage
        {
            Text = "a",
            Html = "<b>a</b>",
            Png = new ReadOnlyMemory<byte>(new byte[] { 1 }),
            Files = ["/x"],
        };

        Assert.Equal(
            HermesDataFormats.Text | HermesDataFormats.Html | HermesDataFormats.Png | HermesDataFormats.Files,
            package.Formats);
        Assert.False(package.IsEmpty);
    }

    [Fact]
    public void CustomOnly_IsNotEmpty_AndHasNoStandardFormats()
    {
        var package = new HermesDataPackage
        {
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/x-test"] = new byte[] { 1 } },
        };

        Assert.False(package.IsEmpty);
        Assert.Equal(HermesDataFormats.None, package.Formats);
    }

    [Fact]
    public void NullCustom_IsReplacedWithEmpty()
    {
        var package = new HermesDataPackage { Custom = null! };

        Assert.NotNull(package.Custom);
        Assert.True(package.IsEmpty);
    }

    [Fact]
    public void Formats_ReportsUrl_AndAUrlAloneIsNotEmpty()
    {
        var package = new HermesDataPackage { Url = new Uri("https://example.com/") };

        Assert.Equal(HermesDataFormats.Url, package.Formats);
        Assert.False(package.IsEmpty);
    }
}
