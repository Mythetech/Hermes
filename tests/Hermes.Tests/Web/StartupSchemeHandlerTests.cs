// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using Hermes.Blazor.Startup;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Hermes.Tests.Web;

public class StartupSchemeHandlerTests
{
    private static StartupSchemeHandler Create()
    {
        var provider = new TestFileProvider()
            .Add("index.html", "<html><body><div id=\"app\"></div></body></html>");
        var page = new InlinedHostPage(new Lazy<IFileProvider>(provider), "index.html");
        return new StartupSchemeHandler(new EarlyStaticContentHandler(new Lazy<IFileProvider>(provider), page));
    }

    [Fact]
    public void Handle_UsesEarlyHandler_BeforeInnerIsSet()
    {
        var handler = Create();

        var (content, contentType) = handler.Handle("app://localhost/");

        Assert.False(handler.HasInner);
        Assert.NotNull(content);
        Assert.Equal("text/html", contentType);
    }

    [Fact]
    public void Handle_ForwardsToInner_OnceSet()
    {
        var handler = Create();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        handler.SetInner(_ => (stream, "application/json"));

        var result = handler.Handle("app://localhost/");

        Assert.True(handler.HasInner);
        Assert.Same(stream, result.Content);
        Assert.Equal("application/json", result.ContentType);
    }

    [Fact]
    public void Handle_PassesUrlThroughToInner()
    {
        var handler = Create();
        string? seenUrl = null;
        handler.SetInner(url => { seenUrl = url; return (null, null); });

        handler.Handle("app://localhost/data.json");

        Assert.Equal("app://localhost/data.json", seenUrl);
    }

    [Fact]
    public void Handle_NeverBlocks_WhenInnerIsNeverSet()
    {
        var handler = Create();
        var stopwatch = Stopwatch.StartNew();

        var (content, contentType) = handler.Handle("app://localhost/missing.js");

        Assert.Null(content);
        Assert.Null(contentType);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, "The startup handler must answer immediately, never wait for the manager.");
    }
}
