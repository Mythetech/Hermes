// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Hermes.Blazor.Startup;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Hermes.Tests.Web;

public class InlinedHostPageTests
{
    private const string HostHtml =
        "<html><body><div id=\"app\"></div><script src=\"_framework/blazor.webview.js\"></script></body></html>";

    private static TestFileProvider ProviderWithHostPage() => new TestFileProvider()
        .Add("index.html", HostHtml)
        .Add("_framework/blazor.webview.js", "console.log('boot')");

    [Fact]
    public void TryGetBytes_InlinesTheFrameworkScript()
    {
        var page = new InlinedHostPage(new Lazy<IFileProvider>(ProviderWithHostPage()), "index.html");

        Assert.True(page.TryGetBytes(out var bytes));
        var html = Encoding.UTF8.GetString(bytes);
        Assert.Contains("<script>console.log('boot')</script>", html);
        Assert.DoesNotContain("src=\"_framework/blazor.webview.js\"", html);
    }

    [Fact]
    public void TryGetBytes_ReturnsFalse_WhenHostPageIsMissing()
    {
        var page = new InlinedHostPage(new Lazy<IFileProvider>(new TestFileProvider()), "index.html");

        Assert.False(page.TryGetBytes(out var bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public void TryGetBytes_ReadsOnce_AndReturnsTheSameArray()
    {
        var page = new InlinedHostPage(new Lazy<IFileProvider>(ProviderWithHostPage()), "index.html");

        page.TryGetBytes(out var first);
        page.TryGetBytes(out var second);

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/index.html", true)]
    [InlineData("/INDEX.HTML", true)]
    [InlineData("/counter", false)]
    [InlineData("/app.css", false)]
    public void Matches_IdentifiesRootAndHostPagePaths(string path, bool expected)
    {
        var page = new InlinedHostPage(new Lazy<IFileProvider>(new TestFileProvider()), "index.html");

        Assert.Equal(expected, page.Matches(path));
    }

    [Fact]
    public void InlinedHostPage_DoesNotResolveTheProvider_UntilBytesAreRequested()
    {
        var created = false;
        var provider = ProviderWithHostPage();
        var lazyProvider = new Lazy<IFileProvider>(() => { created = true; return provider; });
        var page = new InlinedHostPage(lazyProvider, "index.html");

        Assert.False(created);

        page.Matches("/index.html");
        Assert.False(created);

        page.TryGetBytes(out _);
        Assert.True(created);
    }
}
