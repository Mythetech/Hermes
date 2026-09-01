// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Blazor;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Web;

/// <summary>
/// WebKitGTK stalls the UI process for a full 500ms timeout when the WebView is
/// size-allocated (shown) before its first load request: the drawing area waits
/// synchronously for a web process that has not been initialized yet. Issuing the
/// load before the first Show avoids it, so the order of backend calls during
/// Build() is load-bearing for Linux startup time.
/// </summary>
public class StartupNavigationOrderTests
{
    [Theory]
    [InlineData(HermesPlatform.Linux)]
    [InlineData(HermesPlatform.macOS)]
    public void Build_NavigatesToAppBaseUri_BeforeShowingTheWindow(HermesPlatform platform)
    {
        var backend = new RecordingWindowBackend { Platform = platform };

        using var app = BuildApp(backend);

        var calls = backend.Recording.MethodCalls.Select(c => c.MethodName).ToList();
        var initializeIndex = calls.IndexOf("Initialize");
        var navigateIndex = calls.IndexOf("NavigateToUrl");
        var showIndex = calls.IndexOf("Show");

        Assert.True(navigateIndex >= 0, "Build() never issued the initial navigation.");
        Assert.True(showIndex >= 0, "Build() never showed the window.");
        Assert.True(initializeIndex < navigateIndex, "The backend must be initialized before it can navigate.");
        Assert.True(navigateIndex < showIndex, "The initial load must be issued before the window is shown.");
        Assert.Equal(HermesWebViewManager.AppBaseUri, backend.Recording.Navigations.Single());
    }

    [Fact]
    public void Build_DoesNotNavigate_OnWindows()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.Windows };

        using var app = BuildApp(backend);

        Assert.DoesNotContain(backend.Recording.MethodCalls, c => c.MethodName == "NavigateToUrl");
        Assert.Contains(backend.Recording.MethodCalls, c => c.MethodName == "Show");
    }

    [Fact]
    public void Run_DoesNotIssueASecondLoad_WhenBuildAlreadyNavigated()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.Linux };
        using var app = BuildApp(backend);

        RunApp(app);

        Assert.Single(backend.Recording.Navigations);
    }

    [Fact]
    public void Run_IssuesTheLoad_WhenBuildDidNotNavigate()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.Windows };
        using var app = BuildApp(backend);

        RunApp(app);

        Assert.Equal(HermesWebViewManager.AppBaseUri, backend.Recording.Navigations.Single());
    }

    [Fact]
    public void Build_WithFastStartup_DoesNotInitializeOrNavigate()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.Linux };

        using var app = BuildApp(backend, builder => builder.UseFastStartup());

        Assert.DoesNotContain(backend.Recording.MethodCalls, c => c.MethodName is "Initialize" or "NavigateToUrl" or "Show");
    }

    private static DisposableApp BuildApp(RecordingWindowBackend backend, Action<HermesBlazorAppBuilder>? configure = null)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        builder.ForceDevServer(false);
        configure?.Invoke(builder);
        return new DisposableApp(HermesBlazorAppBuilder.BuildForTest(builder, backend));
    }

    /// <summary>
    /// Run() installs the Hermes synchronization context on the calling thread and, with the
    /// recording backend, returns immediately. Restore the previous context afterward so the
    /// test's own continuations do not post into the recording backend's queue.
    /// </summary>
    private static void RunApp(DisposableApp app)
    {
        var previous = SynchronizationContext.Current;
        try
        {
            app.App.Run();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class DisposableApp : IDisposable
    {
        public DisposableApp(HermesBlazorApp app) => App = app;

        public HermesBlazorApp App { get; }

        public void Dispose() => App.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
