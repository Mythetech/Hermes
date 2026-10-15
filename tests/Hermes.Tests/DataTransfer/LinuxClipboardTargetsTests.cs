// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class LinuxClipboardTargetsTests
{
    [Theory]
    [InlineData("UTF8_STRING", HermesDataFormats.Text)]
    [InlineData("text/plain;charset=utf-8", HermesDataFormats.Text)]
    [InlineData("STRING", HermesDataFormats.Text)]
    [InlineData("text/html", HermesDataFormats.Html)]
    [InlineData("image/png", HermesDataFormats.Png)]
    [InlineData("image/jpeg", HermesDataFormats.Png)]
    [InlineData("text/uri-list", HermesDataFormats.Files)]
    [InlineData("x-special/gnome-copied-files", HermesDataFormats.Files)]
    [InlineData("application/x-other", HermesDataFormats.None)]
    public void ToFormats_MapsSingleTargets(string target, HermesDataFormats expected) =>
        Assert.Equal(expected, LinuxClipboardTargets.ToFormats([target]));

    [Fact]
    public void ToFormats_FileIconImage_DoesNotCountAsPng() =>
        Assert.Equal(HermesDataFormats.Files, LinuxClipboardTargets.ToFormats(["x-special/gnome-copied-files", "image/x-icon"]));

    [Fact]
    public void ToFormats_RealPngBesideFiles_CountsAsPng() =>
        Assert.Equal(HermesDataFormats.Files | HermesDataFormats.Png, LinuxClipboardTargets.ToFormats(["text/uri-list", "image/png"]));

    [Fact]
    public void ForWrite_OffersEveryTargetForAFullPackage()
    {
        var file = Path.Combine(Path.GetTempPath(), "a b.txt");
        var package = new HermesDataPackage
        {
            Text = "t",
            Html = "<b>t</b>",
            Png = new ReadOnlyMemory<byte>(new byte[] { 1 }),
            Files = [file],
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/x-hermes"] = new byte[] { 9 } },
        };

        var entries = LinuxClipboardTargets.ForWrite(package);

        Assert.Equal(
            new[] { "UTF8_STRING", "text/plain;charset=utf-8", "text/plain", "text/html", "image/png", "text/uri-list", "x-special/gnome-copied-files", "application/x-hermes" },
            entries.Select(e => e.Target));
        Assert.Equal("t", Encoding.UTF8.GetString(entries[0].Data));
        Assert.Equal(new[] { file }, FileUriList.ParseUriList(Encoding.UTF8.GetString(entries[5].Data)));
        Assert.StartsWith("copy\n", Encoding.UTF8.GetString(entries[6].Data));
        Assert.Equal(new byte[] { 9 }, entries[7].Data);
    }

    [Fact]
    public void ForWrite_StandardTargetWinsOverCollidingCustomKey()
    {
        var package = new HermesDataPackage
        {
            Files = [Path.Combine(Path.GetTempPath(), "a.txt")],
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["x-special/gnome-copied-files"] = new byte[] { 9 } },
        };

        var entries = LinuxClipboardTargets.ForWrite(package);

        var gnome = Assert.Single(entries, e => e.Target == "x-special/gnome-copied-files");
        Assert.StartsWith("copy\n", Encoding.UTF8.GetString(gnome.Data));
    }

    [Fact]
    public void DecodeHtml_HandlesUtf16BomUtf8BomAndTrailingNul()
    {
        const string html = "<b>é 世界</b>";

        Assert.Equal(html, LinuxClipboardTargets.DecodeHtml(Encoding.UTF8.GetBytes(html + "\0")));
        Assert.Equal(html, LinuxClipboardTargets.DecodeHtml([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(html)]));
        Assert.Equal(html, LinuxClipboardTargets.DecodeHtml([0xFF, 0xFE, .. Encoding.Unicode.GetBytes(html)]));
        Assert.Equal(html, LinuxClipboardTargets.DecodeHtml([0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(html)]));
    }

    [Fact]
    public void ToFormats_ReportsAUrlFromTheLinkTarget_AndItsUriListIsNotFiles() =>
        Assert.Equal(HermesDataFormats.Url, LinuxClipboardTargets.ToFormats(["text/uri-list", "_NETSCAPE_URL"]));

    [Fact]
    public void ToFormats_FileManagerCopy_ReportsFilesWithoutAUrl() =>
        Assert.Equal(HermesDataFormats.Files, LinuxClipboardTargets.ToFormats(["text/uri-list", "x-special/gnome-copied-files"]));

    [Fact]
    public void ForWrite_PutsFilesThenTheUrlInOneUriList_AndGnomeCopiedFilesKeepsOnlyFiles()
    {
        var file = Path.Combine(Path.GetTempPath(), "a b.txt");
        var url = new Uri("https://example.com/a b?c=d#e");

        var entries = LinuxClipboardTargets.ForWrite(new HermesDataPackage { Files = [file], Url = url });

        Assert.Equal(new[] { "text/uri-list", "x-special/gnome-copied-files", "_NETSCAPE_URL" }, entries.Select(e => e.Target));
        var (files, listed) = FileUriList.SplitUriList(Encoding.UTF8.GetString(entries[0].Data));
        Assert.Equal(new[] { file }, files);
        Assert.Equal(url.AbsoluteUri, listed?.AbsoluteUri);
        Assert.Equal(new[] { file }, FileUriList.ParseGnomeCopiedFiles(Encoding.UTF8.GetString(entries[1].Data)));
        Assert.Equal(url.AbsoluteUri, Encoding.UTF8.GetString(entries[2].Data));
        Assert.Equal(HermesDataFormats.Files | HermesDataFormats.Url, LinuxClipboardTargets.ToFormats([.. entries.Select(e => e.Target)]));
    }

    [Fact]
    public void ForWrite_UrlAlone_ReportsOnlyUrl()
    {
        var entries = LinuxClipboardTargets.ForWrite(new HermesDataPackage { Text = "link", Url = new Uri("https://example.com/") });

        Assert.Equal("https://example.com/\r\n", Encoding.UTF8.GetString(Assert.Single(entries, e => e.Target == "text/uri-list").Data));
        Assert.Equal(HermesDataFormats.Text | HermesDataFormats.Url, LinuxClipboardTargets.ToFormats([.. entries.Select(e => e.Target)]));
    }

    [Theory]
    [InlineData("https://example.com/link\nExample", "https://example.com/link")]
    [InlineData("https://example.com/link\0", "https://example.com/link")]
    [InlineData("file:///tmp/a.txt\nhttps://example.com/", null)]
    [InlineData("not a url", null)]
    public void ParseNetscapeUrl_ReadsTheFirstLine(string data, string? expected) =>
        Assert.Equal(expected, LinuxClipboardTargets.ParseNetscapeUrl(Encoding.UTF8.GetBytes(data))?.AbsoluteUri);
}
