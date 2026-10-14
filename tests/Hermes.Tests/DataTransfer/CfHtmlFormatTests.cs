// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class CfHtmlFormatTests
{
    [Theory]
    [InlineData("<b>bold</b>")]
    [InlineData("<p>héllo 世界 🎉</p>")]
    public void Build_ThenParse_ReturnsTheFragment(string fragment)
    {
        var data = CfHtmlFormat.Build(fragment);

        Assert.Equal(fragment, CfHtmlFormat.Parse(data));
    }

    [Fact]
    public void Build_OffsetsPointAtTheDocumentAndFragment()
    {
        var fragment = "<i>é</i>";
        var data = CfHtmlFormat.Build(fragment);
        var text = Encoding.UTF8.GetString(data);

        int Offset(string key)
        {
            var start = text.IndexOf(key, StringComparison.Ordinal) + key.Length;
            return int.Parse(text.Substring(start, 10));
        }

        Assert.StartsWith("<html>", Encoding.UTF8.GetString(data, Offset("StartHTML:"), 6));
        Assert.Equal(data.Length, Offset("EndHTML:"));
        var fragmentBytes = data.AsSpan(Offset("StartFragment:"), Offset("EndFragment:") - Offset("StartFragment:"));
        Assert.Equal(fragment, Encoding.UTF8.GetString(fragmentBytes));
    }

    [Fact]
    public void Parse_IgnoresATrailingNul()
    {
        var data = CfHtmlFormat.Build("<b>x</b>").Concat(new byte[] { 0, 0 }).ToArray();

        Assert.Equal("<b>x</b>", CfHtmlFormat.Parse(data));
    }

    [Fact]
    public void Parse_FallsBackToMarkers_WhenOffsetsAreMinusOne()
    {
        var data = Encoding.UTF8.GetBytes(
            "Version:0.9\r\nStartHTML:-1\r\nEndHTML:-1\r\nStartFragment:-1\r\nEndFragment:-1\r\n" +
            "<html><body><!--StartFragment--><b>x</b><!--EndFragment--></body></html>");

        Assert.Equal("<b>x</b>", CfHtmlFormat.Parse(data));
    }

    [Fact]
    public void Parse_Garbage_ReturnsNull() =>
        Assert.Null(CfHtmlFormat.Parse(Encoding.UTF8.GetBytes("not cf_html")));
}
