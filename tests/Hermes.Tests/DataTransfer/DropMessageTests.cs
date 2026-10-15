// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class DropMessageTests
{
    [Fact]
    public void TryParse_ReadsADrop()
    {
        var message = DropMessage.TryParse(
            """{"type":"hermes-drop","seq":7,"x":12.5,"y":40,"text":"hi","html":"<b>hi</b>","uriList":"https://example.com/"}""");

        Assert.NotNull(message);
        Assert.Equal(HermesDropReportKind.Drop, message.Kind);
        Assert.Equal(7, message.Sequence);
        Assert.Equal(12.5, message.X);
        Assert.Equal(40, message.Y);
        Assert.Equal("hi", message.Text);
        Assert.Equal("<b>hi</b>", message.Html);
        Assert.Equal("https://example.com/", message.UriList);
    }

    [Fact]
    public void TryParse_ReadsAReset() =>
        Assert.Equal(HermesDropReportKind.Reset, DropMessage.TryParse("""{"type":"hermes-drop-reset"}""")?.Kind);

    [Theory]
    [InlineData("""{"type":"hermes-drop"}""")]
    [InlineData("""{"type":"hermes-drop","seq":"7"}""")]
    [InlineData("""{"type":"hermes-drop-ready","seq":1}""")]
    [InlineData("""{"type":"hermes-drop-anything"}""")]
    [InlineData("""{"type":7}""")]
    [InlineData("""{"type":"hermes-drop",""")]
    [InlineData("""[]""")]
    public void TryParse_ReturnsNullForMalformedMessages(string json) =>
        Assert.Null(DropMessage.TryParse(json));

    [Theory]
    [InlineData("""{"type":"hermes-drop","seq":1,"x":2,"y":3}""", true)]
    [InlineData("""{"type":"hermes-drop-reset"}""", true)]
    [InlineData("""{"type":"hermes-drop-anything","seq":"x"}""", true)]
    [InlineData("""{"type":"hermes-drag","action":"drag"}""", false)]
    [InlineData("""{"type":"pong"}""", false)]
    [InlineData("__bwv:[]", false)]
    [InlineData(null, false)]
    public void IsDropProtocol_MatchesEveryDropScriptMessage(string? message, bool expected) =>
        Assert.Equal(expected, DropMessage.IsDropProtocol(message));

    [Fact]
    public void ToPackage_TakesTheFirstNonFileUrl_AndLeavesEmptyStringsNull()
    {
        var message = DropMessage.TryParse(
            """{"type":"hermes-drop","seq":1,"x":0,"y":0,"text":"","html":"","uriList":"# c\r\nfile:///C:/a.txt\r\nhttps://example.com/x\r\n"}""")!;

        var package = message.ToPackage([]);

        Assert.Null(package.Text);
        Assert.Null(package.Html);
        Assert.Null(package.Files);
        Assert.Equal("https://example.com/x", package.Url?.AbsoluteUri);
    }

    [Fact]
    public void ToPackage_CarriesFilePaths()
    {
        var path = Path.Combine(Path.GetTempPath(), "a.txt");
        var package = DropMessage.TryParse("""{"type":"hermes-drop","seq":1,"x":0,"y":0}""")!.ToPackage([path]);

        Assert.Equal(new[] { path }, package.Files);
    }

    [Fact]
    public void ToReport_CopiesTheMessageAndData()
    {
        var data = new HermesDataPackage { Text = "x" };
        var report = DropMessage.TryParse("""{"type":"hermes-drop","seq":3,"x":1,"y":2}""")!.ToReport(data);

        Assert.Equal(HermesDropReportKind.Drop, report.Kind);
        Assert.Equal(3, report.Sequence);
        Assert.Equal(1, report.X);
        Assert.Equal(2, report.Y);
        Assert.Same(data, report.Data);
    }
}
