// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Xunit;

namespace Hermes.Tests.Web;

public class HostPageInlinerTests
{
    private const string HostPage = """
        <!DOCTYPE html>
        <html>
        <head><title>App</title></head>
        <body>
            <div id="app"></div>
            <script src="_framework/blazor.webview.js"></script>
        </body>
        </html>
        """;

    [Fact]
    public void Inline_ReplacesScriptTagWithContent()
    {
        var result = HostPageInliner.Inline(HostPage, _ => "console.log('blazor');");

        Assert.DoesNotContain("src=\"_framework/blazor.webview.js\"", result);
        Assert.Contains("<script>console.log('blazor');</script>", result);
    }

    [Fact]
    public void Inline_RequestsTheBlazorWebViewAsset()
    {
        string? requested = null;

        HostPageInliner.Inline(HostPage, path => { requested = path; return "x"; });

        Assert.Equal("_framework/blazor.webview.js", requested);
    }

    [Fact]
    public void Inline_ReturnsUnchanged_WhenMarkerAbsent()
    {
        var html = "<html><body><p>no scripts</p></body></html>";

        var result = HostPageInliner.Inline(html, _ => "x");

        Assert.Equal(html, result);
    }

    [Fact]
    public void Inline_ReturnsUnchanged_WhenResolverReturnsNull()
    {
        var result = HostPageInliner.Inline(HostPage, _ => null);

        Assert.Equal(HostPage, result);
    }

    [Fact]
    public void Inline_HandlesSingleQuotesAndWhitespace()
    {
        var html = "<body><script   src='_framework/blazor.webview.js'  ></script></body>";

        var result = HostPageInliner.Inline(html, _ => "js();");

        Assert.DoesNotContain("_framework/blazor.webview.js", result);
        Assert.Contains("<script>js();</script>", result);
    }
}
