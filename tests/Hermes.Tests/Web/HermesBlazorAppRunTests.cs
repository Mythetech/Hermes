// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Blazor.Threading;
using Hermes.Testing;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Hermes.Tests.Web;

public class HermesBlazorAppRunTests
{
    [Fact]
    public async Task Run_NavigatesViaThePostedInitializationTask_AndDisposeObservesIt()
    {
        var backend = new RecordingWindowBackend();
        var app = CreateApp(backend);

        app.Run();

        Assert.Contains(backend.Recording.Navigations, n => n.Contains("localhost"));
        Assert.Contains(backend.Recording.MethodCalls, c => c.MethodName == "WaitForClose");

        await app.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_Completes_WhenRunWasNeverCalled()
    {
        var backend = new RecordingWindowBackend();
        var app = CreateApp(backend);

        await app.DisposeAsync();
    }

    private static HermesBlazorApp CreateApp(RecordingWindowBackend backend)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        var window = composition.ServiceProvider.GetService(typeof(HermesWindow)) as HermesWindow
            ?? throw new InvalidOperationException("Window not registered");
        var syncContext = composition.ServiceProvider.GetService(typeof(HermesSynchronizationContext)) as HermesSynchronizationContext
            ?? throw new InvalidOperationException("Sync context not registered");
        var dispatcher = composition.ServiceProvider.GetService(typeof(HermesDispatcher)) as HermesDispatcher
            ?? throw new InvalidOperationException("Dispatcher not registered");

        var webViewManager = new HermesWebViewManager(
            backend,
            composition.ServiceProvider,
            dispatcher,
            composition.FileProvider,
            new JSComponentConfigurationStore(),
            "index.html",
            baseUri: null,
            isDevMode: false);

        return new HermesBlazorApp(
            composition.ServiceProvider,
            builder.Configuration,
            window,
            webViewManager,
            syncContext,
            windowShownDuringBuild: false);
    }
}
