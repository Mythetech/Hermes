// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Web.Hosting;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class FrontendResolverTests : IDisposable
{
    private static readonly Uri DevServer = new("http://localhost:5173");
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hermes-frontend-{Guid.NewGuid():N}", "www & root");

    [Fact]
    public void Development_ListeningDevServer_UsesIt()
    {
        var source = FrontendResolver.Resolve(true, DevServer, _root, Timeout, (_, _) => true);

        var devServer = Assert.IsType<FrontendSource.DevServer>(source);
        Assert.Equal(DevServer, devServer.Url);
    }

    [Fact]
    public void Development_SilentDevServer_FallsBackToStaticFiles()
    {
        WriteIndex();

        var source = FrontendResolver.Resolve(true, DevServer, _root, Timeout, (_, _) => false);

        var staticFiles = Assert.IsType<FrontendSource.StaticFiles>(source);
        Assert.Equal(_root, staticFiles.Root);
    }

    [Fact]
    public void Production_NeverProbes_UsesStaticFiles()
    {
        WriteIndex();

        var source = FrontendResolver.Resolve(false, DevServer, _root, Timeout,
            (_, _) => throw new InvalidOperationException("Probed outside Development."));

        Assert.IsType<FrontendSource.StaticFiles>(source);
    }

    [Fact]
    public void NoIndexHtml_IsMissing_AndNamesBothOptions()
    {
        var source = FrontendResolver.Resolve(true, DevServer, _root, Timeout, (_, _) => false);

        var missing = Assert.IsType<FrontendSource.Missing>(source);
        Assert.Contains("http://localhost:5173", missing.Html);
        Assert.Contains("www &amp; root", missing.Html);
        Assert.Contains("rebuild the app", missing.Html);
        Assert.DoesNotContain("build the frontend into", missing.Html);
    }

    [Fact]
    public void Production_NoIndexHtml_SaysToPublish()
    {
        var source = FrontendResolver.Resolve(false, DevServer, _root, Timeout, (_, _) => true);

        var missing = Assert.IsType<FrontendSource.Missing>(source);
        Assert.Contains("dotnet publish", missing.Html);
        Assert.DoesNotContain("5173", missing.Html);
    }

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
    }

    private void WriteIndex()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "index.html"), "<p>app</p>");
    }
}
