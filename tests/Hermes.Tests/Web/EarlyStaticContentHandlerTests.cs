// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.Startup;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Hermes.Tests.Web;

public class EarlyStaticContentHandlerTests
{
    private const string HostHtml =
        "<html><body><div id=\"app\"></div><script src=\"_framework/blazor.webview.js\"></script></body></html>";

    private static (EarlyStaticContentHandler Handler, TestFileProvider Provider) Create(bool withHostPage = true)
    {
        var provider = new TestFileProvider()
            .Add("_framework/blazor.webview.js", "console.log('boot')")
            .Add("_framework/blazor.modules.json", "[]")
            .Add("css/app.css", "body { }");
        if (withHostPage)
            provider.Add("index.html", HostHtml);

        var page = new InlinedHostPage(new Lazy<IFileProvider>(provider), "index.html");
        return (new EarlyStaticContentHandler(new Lazy<IFileProvider>(provider), page), provider);
    }

    private static string ReadAll(Stream? stream)
    {
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("app://localhost/")]
    [InlineData("app://localhost/counter")]
    [InlineData("app://localhost/index.html")]
    [InlineData("app://localhost/?v=1")]
    public void Handle_ServesInlinedHostPage_ForRootRoutesAndHostPage(string url)
    {
        var (handler, _) = Create();

        var (content, contentType) = handler.Handle(url);

        Assert.Equal("text/html", contentType);
        Assert.Contains("<script>console.log('boot')</script>", ReadAll(content));
    }

    [Fact]
    public void Handle_ServesStaticFile_WithNullContentType_AndIgnoresQueryString()
    {
        var (handler, _) = Create();

        var (content, contentType) = handler.Handle("app://localhost/_framework/blazor.modules.json?v=1");

        Assert.Null(contentType);
        Assert.Equal("[]", ReadAll(content));
    }

    [Fact]
    public void Handle_Returns404_ForMissingFile()
    {
        var (handler, _) = Create();

        var (content, contentType) = handler.Handle("app://localhost/missing.js");

        Assert.Null(content);
        Assert.Null(contentType);
    }

    [Fact]
    public void Handle_Returns404_ForRoute_WhenHostPageIsMissing()
    {
        var (handler, _) = Create(withHostPage: false);

        var (content, contentType) = handler.Handle("app://localhost/counter");

        Assert.Null(content);
        Assert.Null(contentType);
    }

    [Fact]
    public void Handle_Returns404_ForMalformedUrl()
    {
        var (handler, _) = Create();

        var (content, contentType) = handler.Handle("not a url");

        Assert.Null(content);
        Assert.Null(contentType);
    }

    [Fact]
    public void EarlyStaticContentHandler_DoesNotResolveTheProvider_UntilARequestArrives()
    {
        var created = false;
        var backingProvider = new TestFileProvider()
            .Add("index.html", HostHtml)
            .Add("_framework/blazor.webview.js", "console.log('boot')");
        var page = new InlinedHostPage(new Lazy<IFileProvider>(backingProvider), "index.html");
        var lazyProvider = new Lazy<IFileProvider>(() => { created = true; return backingProvider; });
        var handler = new EarlyStaticContentHandler(lazyProvider, page);

        Assert.False(created);

        // A path with an extension that is not the host page reaches the
        // provider branch of Handle, unlike the root route used elsewhere.
        var (content, _) = handler.Handle("app://localhost/_framework/blazor.webview.js");
        content?.Dispose();

        Assert.True(created);
    }
}
