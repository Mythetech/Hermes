// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Runtime.Versioning;
using System.Text;
using Hermes.Contracts.DataTransfer;
using Hermes.Platforms.Windows;
using Windows.Win32;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("windows")]
public sealed class WindowsClipboardFormatsTests
{
    [Fact]
    public void EncodeFormats_WritesTheUrlAsNullTerminatedUtf16()
    {
        var (format, data) = Assert.Single(WindowsClipboardBackend.EncodeFormats(
            new HermesDataPackage { Url = new Uri("https://example.com/a b") }));

        Assert.Equal(PInvoke.RegisterClipboardFormat("UniformResourceLocatorW"), format);
        Assert.Equal(Encoding.Unicode.GetBytes("https://example.com/a%20b\0"), data.ToArray());
    }

    [Fact]
    public void EncodeFormats_KeepsTheClipboardWriteOrder()
    {
        var formats = WindowsClipboardBackend.EncodeFormats(new HermesDataPackage
        {
            Text = "t",
            Html = "<b>h</b>",
            Files = [@"C:\a.txt"],
            Url = new Uri("https://example.com/"),
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/x-hermes-test"] = new byte[] { 1 } },
        }).Select(entry => entry.Format);

        Assert.Equal(
            new[]
            {
                13u,
                PInvoke.RegisterClipboardFormat("HTML Format"),
                15u,
                PInvoke.RegisterClipboardFormat("Preferred DropEffect"),
                PInvoke.RegisterClipboardFormat("UniformResourceLocatorW"),
                PInvoke.RegisterClipboardFormat("application/x-hermes-test"),
            },
            formats);
    }

    [Theory]
    [InlineData("https://example.com/x", "https://example.com/x")]
    [InlineData(" https://example.com/x \r\n", "https://example.com/x")]
    [InlineData("file:///C:/a.txt", null)]
    [InlineData(@"C:\a.txt", null)]
    [InlineData("not a url", null)]
    [InlineData("", null)]
    public void ParseUrl_KeepsOnlyAbsoluteNonFileUrls(string text, string? expected) =>
        Assert.Equal(expected, WindowsClipboardBackend.ParseUrl(Encoding.Unicode.GetBytes(text + "\0"))?.AbsoluteUri);
}
#endif
