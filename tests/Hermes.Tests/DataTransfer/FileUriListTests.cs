// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class FileUriListTests
{
    private static readonly string[] s_paths =
    [
        Path.Combine(Path.GetTempPath(), "plain.txt"),
        Path.Combine(Path.GetTempPath(), "with space", "a#b%c.txt"),
        Path.Combine(Path.GetTempPath(), "naïve 世界.png"),
    ];

    [Fact]
    public void UriList_RoundTripsSpecialCharacters() =>
        Assert.Equal(s_paths, FileUriList.ParseUriList(FileUriList.BuildUriList(s_paths)));

    [Fact]
    public void GnomeCopiedFiles_RoundTripsSpecialCharacters() =>
        Assert.Equal(s_paths, FileUriList.ParseGnomeCopiedFiles(FileUriList.BuildGnomeCopiedFiles(s_paths)));

    [Fact]
    public void BuildUriList_EndsEachLineWithCrLf()
    {
        var list = FileUriList.BuildUriList(s_paths);

        Assert.Equal(s_paths.Length, list.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.EndsWith("\r\n", list);
        Assert.StartsWith("file://", list);
    }

    [Fact]
    public void ToFileUri_EscapesHashAndPercent()
    {
        var uri = FileUriList.ToFileUri(s_paths[1]);

        Assert.Contains("a%23b%25c.txt", uri);
    }

    [Fact]
    public void BackslashInFileName_RoundTripsOnNonWindows()
    {
        if (OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(Path.GetTempPath(), "a\\b.txt");

        Assert.Equal(new[] { path }, FileUriList.ParseUriList(FileUriList.BuildUriList(new[] { path })));
        Assert.Contains("a%5Cb.txt", FileUriList.ToFileUri(path));
    }

    [Fact]
    public void ParseUriList_SkipsCommentsBlankLinesAndNonFileUris()
    {
        var text = "# comment\r\n\r\nhttps://example.com/x\r\n" + FileUriList.ToFileUri(s_paths[0]) + "\r\n";

        Assert.Equal(new[] { s_paths[0] }, FileUriList.ParseUriList(text));
    }

    [Fact]
    public void ParseGnomeCopiedFiles_SkipsCutLine() =>
        Assert.Equal(new[] { s_paths[0] }, FileUriList.ParseGnomeCopiedFiles("cut\n" + FileUriList.ToFileUri(s_paths[0])));

    [Fact]
    public void FromFileUri_AcceptsLocalhost_RejectsOtherHosts()
    {
        Assert.Equal("/tmp/x", FileUriList.FromFileUri("file://localhost/tmp/x"));
        Assert.Null(FileUriList.FromFileUri("file://otherhost/tmp/x"));
        Assert.Null(FileUriList.FromFileUri("https://example.com/x"));
    }

    [Fact]
    public void SplitUriList_SeparatesFilesFromTheUrl()
    {
        var list = FileUriList.BuildUriList(s_paths, new Uri("https://example.com/a b"));

        var (files, url) = FileUriList.SplitUriList(list);

        Assert.Equal(s_paths, files);
        Assert.Equal("https://example.com/a%20b", url?.AbsoluteUri);
    }

    [Fact]
    public void FirstUrl_SkipsCommentsBlankLinesAndFileUris() =>
        Assert.Equal("https://example.com/", FileUriList.FirstUrl(
            "# comment\r\n\r\nfile:///tmp/a.txt\r\nhttps://example.com/\r\nhttps://second.example/\r\n")?.AbsoluteUri);

    [Fact]
    public void FirstUrl_ReturnsNullForAFileOnlyList() =>
        Assert.Null(FileUriList.FirstUrl(FileUriList.BuildUriList(s_paths)));

    [Fact]
    public void BuildUriList_WithOnlyAUrl_WritesOneLine() =>
        Assert.Equal("https://example.com/\r\n", FileUriList.BuildUriList(null, new Uri("https://example.com/")));
}
