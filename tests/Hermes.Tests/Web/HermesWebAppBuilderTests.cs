// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Net;
using Hermes.Testing;
using Hermes.Web;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class HermesWebAppBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hermes-web-{Guid.NewGuid():N}");

    private static string BaseUri => OperatingSystem.IsWindows() ? "http://localhost/" : "app://localhost/";

    private static string StaticScheme => OperatingSystem.IsWindows() ? "http" : "app";

    [Fact]
    public void UseFrontend_RejectsADevServerWithoutScheme()
    {
        var builder = HermesWebAppBuilder.CreateSlimBuilder();

        Assert.Throws<ArgumentException>(() => builder.UseFrontend(devServer: "localhost:5173"));
    }

    [Fact]
    public void UseFrontend_RejectsARelativeUrl()
    {
        var builder = HermesWebAppBuilder.CreateSlimBuilder();

        Assert.Throws<ArgumentException>(() => builder.UseFrontend(devServer: "/dev"));
    }

    [Fact]
    public async Task Build_Development_ListeningDevServer_NavigatesToIt()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesWebAppBuilder.CreateSlimBuilder(["--environment", "Development"]);
        builder.ProbeDevServer = (_, _) => true;
        builder.UseFrontend(devServer: "http://localhost:5173");

        var app = HermesWebAppBuilder.BuildForTest(builder, backend);
        WebAppRunner.Run(app);

        Assert.True(backend.Recording.NavigatedTo("http://localhost:5173/"));
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Build_Production_ServesStaticFiles()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "index.html"), "<p>app</p>");
        var backend = new RecordingWindowBackend();
        var builder = HermesWebAppBuilder.CreateSlimBuilder(["--environment", "Production"]);
        builder.ProbeDevServer = (_, _) => throw new InvalidOperationException("Probed outside Development.");
        builder.UseFrontend(devServer: "http://localhost:5173", options => options.StaticFilesPath = _root);

        var app = HermesWebAppBuilder.BuildForTest(builder, backend);
        WebAppRunner.Run(app);

        Assert.True(backend.Recording.NavigatedTo(BaseUri));
        var (content, _) = backend.TestCustomScheme(StaticScheme, BaseUri + "index.html")!.Value;
        using var reader = new StreamReader(content!);
        Assert.Equal("<p>app</p>", reader.ReadToEnd());
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Build_WithoutUseFrontend_LooksInWwwrootUnderTheBaseDirectory()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesWebAppBuilder.CreateSlimBuilder(["--environment", "Production"]);

        var app = HermesWebAppBuilder.BuildForTest(builder, backend);
        WebAppRunner.Run(app);

        var expected = WebUtility.HtmlEncode(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        Assert.Contains(expected, backend.InitialOptions!.StartHtml);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Build_NothingToServe_ShowsTheMissingPage()
    {
        Directory.CreateDirectory(_root);
        var backend = new RecordingWindowBackend();
        var builder = HermesWebAppBuilder.CreateSlimBuilder(["--environment", "Production"]);
        builder.UseFrontend(configure: options => options.StaticFilesPath = _root);

        var app = HermesWebAppBuilder.BuildForTest(builder, backend);
        WebAppRunner.Run(app);

        Assert.Contains("dotnet publish", backend.InitialOptions!.StartHtml);
        Assert.Empty(backend.Recording.Navigations);
        await app.DisposeAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

internal static class WebAppRunner
{
    /// <summary>Runs the app, then puts back the test thread's synchronization context.</summary>
    public static void Run(HermesWebApp app)
    {
        var previous = SynchronizationContext.Current;
        try
        {
            app.Run();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
