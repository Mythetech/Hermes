// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Web.Hosting;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class StaticFileHostTests
{
    private static (StaticFileHost Host, string TempDir) CreateHost(bool spaFallback = false)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        File.WriteAllText(Path.Combine(tempDir, "index.html"), "<html>hello</html>");

        var assetsDir = Path.Combine(tempDir, "assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "app.js"), "console.log('hi');");

        return (new StaticFileHost(tempDir, spaFallback), tempDir);
    }

    private static void Cleanup(string tempDir)
    {
        try { Directory.Delete(tempDir, true); }
        catch { /* best effort */ }
    }

    private static string ReadStream(Stream? stream)
    {
        if (stream is null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void HandleRequest_Root_ReturnsIndexHtml()
    {
        var (host, dir) = CreateHost();
        try
        {
            var (content, contentType) = host.HandleRequest("/");

            Assert.NotNull(content);
            Assert.Equal("text/html", contentType);
            Assert.Equal("<html>hello</html>", ReadStream(content));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_IndexHtml_ReturnsFileWithCorrectType()
    {
        var (host, dir) = CreateHost();
        try
        {
            var (content, contentType) = host.HandleRequest("/index.html");

            Assert.NotNull(content);
            Assert.Equal("text/html", contentType);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_NestedFile_ReturnsCorrectContentAndType()
    {
        var (host, dir) = CreateHost();
        try
        {
            var (content, contentType) = host.HandleRequest("/assets/app.js");

            Assert.NotNull(content);
            Assert.Equal("application/javascript", contentType);
            Assert.Equal("console.log('hi');", ReadStream(content));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_NonExistentFile_ReturnsNull()
    {
        var (host, dir) = CreateHost();
        try
        {
            var (content, contentType) = host.HandleRequest("/missing.js");

            Assert.Null(content);
            Assert.Null(contentType);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_DirectoryTraversal_ReturnsNull()
    {
        var (host, dir) = CreateHost();
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "secret.txt"), "secret");

            var (content, contentType) = host.HandleRequest("/../secret.txt");

            Assert.Null(content);
            Assert.Null(contentType);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_QueryString_StrippedBeforeResolution()
    {
        var (host, dir) = CreateHost();
        try
        {
            var (content, contentType) = host.HandleRequest("/assets/app.js?v=123");

            Assert.NotNull(content);
            Assert.Equal("application/javascript", contentType);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_FullUrlWithScheme_ExtractsPathCorrectly()
    {
        var (host, dir) = CreateHost();
        try
        {
            var (content, contentType) = host.HandleRequest("http://localhost/assets/app.js");

            Assert.NotNull(content);
            Assert.Equal("application/javascript", contentType);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_SpaFallbackDisabled_ExtensionlessPathReturnsNull()
    {
        var (host, dir) = CreateHost(spaFallback: false);
        try
        {
            var (content, contentType) = host.HandleRequest("/about");

            Assert.Null(content);
            Assert.Null(contentType);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_SpaFallbackEnabled_ExtensionlessPathReturnsIndexHtml()
    {
        var (host, dir) = CreateHost(spaFallback: true);
        try
        {
            var (content, contentType) = host.HandleRequest("/about");

            Assert.NotNull(content);
            Assert.Equal("text/html", contentType);
            Assert.Equal("<html>hello</html>", ReadStream(content));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_SpaFallbackEnabled_MissingFileWithExtensionReturnsNull()
    {
        var (host, dir) = CreateHost(spaFallback: true);
        try
        {
            var (content, contentType) = host.HandleRequest("/missing.js");

            Assert.Null(content);
            Assert.Null(contentType);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void HandleRequest_CustomSchemeUrl_ExtractsPathCorrectly()
    {
        var (host, dir) = CreateHost();
        try
        {
            var (content, contentType) = host.HandleRequest("app://localhost/index.html");

            Assert.NotNull(content);
            Assert.Equal("text/html", contentType);
        }
        finally { Cleanup(dir); }
    }
}
