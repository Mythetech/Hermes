// Copyright (c) Mythetech. Licensed under the MIT License.
#if MACOS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Contracts.DataTransfer;
using Hermes.Platforms.macOS;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("macos")]
public sealed class MacNativePackageTests
{
    [Fact]
    public void Allocate_ThenRead_RoundTripsEveryFormat()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hermes package #1");
        var package = new HermesDataPackage
        {
            Text = "plain é",
            Html = "<b>rich</b>",
            Url = new Uri("https://example.com/a b?c=d#e"),
            Png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 7 },
            Files = [Path.Combine(directory, "a.txt"), Path.Combine(directory, "b é.txt")],
            Custom = new Dictionary<string, ReadOnlyMemory<byte>>
            {
                ["application/x-hermes-a"] = new byte[] { 1, 2, 250 },
                ["application/x-hermes-empty"] = Array.Empty<byte>(),
            },
        };

        var native = MacNativePackage.Allocate(package);
        try
        {
            var read = MacNativePackage.Read(native);

            Assert.Equal(package.Text, read.Text);
            Assert.Equal(package.Html, read.Html);
            Assert.Equal("https://example.com/a%20b?c=d#e", read.Url?.AbsoluteUri);
            Assert.Equal(package.Png.Value.ToArray(), read.Png?.ToArray());
            Assert.Equal(package.Files, read.Files);
            Assert.Equal(new byte[] { 1, 2, 250 }, read.Custom["application/x-hermes-a"].ToArray());
            Assert.Empty(read.Custom["APPLICATION/X-HERMES-EMPTY"].ToArray());
        }
        finally
        {
            MacNativePackage.Free(ref native);
        }
    }

    [Fact]
    public void Read_OfAnEmptyNativePackage_IsEmpty()
    {
        var read = MacNativePackage.Read(default);

        Assert.True(read.IsEmpty);
        Assert.Null(read.Png);
        Assert.Null(read.Files);
    }

    [Fact]
    public void Free_ClearsThePackage_AndToleratesASecondCall()
    {
        var native = MacNativePackage.Allocate(new HermesDataPackage { Text = "x", Files = ["/tmp/a.txt"] });

        MacNativePackage.Free(ref native);
        MacNativePackage.Free(ref native);

        Assert.True(MacNativePackage.Read(native).IsEmpty);
    }

    [Theory]
    [InlineData("https://example.com/a%20b?c=d#e", "https://example.com/a%20b?c=d#e")]
    [InlineData("mailto:someone@example.com", "mailto:someone@example.com")]
    [InlineData("relative/thing", null)]
    [InlineData("file:///tmp/a.txt", null)]
    [InlineData("/tmp/a.txt", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseUrl_KeepsOnlyAbsoluteNonFileUrls(string? value, string? expected) =>
        Assert.Equal(expected, MacNativePackage.ParseUrl(value)?.AbsoluteUri);

    // HermesTypes.h asserts the same size and offsets when the native library compiles.
    [Fact]
    public void Layout_MatchesTheNativeStruct()
    {
        Assert.Equal(88, Marshal.SizeOf<HermesNativePackage>());
        Assert.Equal(40, (int)Marshal.OffsetOf<HermesNativePackage>(nameof(HermesNativePackage.Files)));
        Assert.Equal(80, (int)Marshal.OffsetOf<HermesNativePackage>(nameof(HermesNativePackage.CustomCount)));
    }
}
#endif
