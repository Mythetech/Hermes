// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Blazor;
using Hermes.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hermes.Tests.Web;

/// <summary>
/// While the composition worker runs, Build() must keep servicing the native
/// event loop so the WebView's process launch and page load progress, and any
/// IPC message that arrives in that window must reach the manager afterwards.
/// </summary>
public class StartupPumpTests
{
    private const string HostHtml =
        "<html><body><div id=\"app\"></div><script src=\"_framework/blazor.webview.js\"></script></body></html>";

    [Fact]
    public void Build_PumpsTheEventLoop_WhileCompositionRuns()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };
        using var release = new ManualResetEventSlim(false);
        backend.EventLoopIterated += release.Set;

        using var app = BuildApp(backend, builder =>
            ((IHostApplicationBuilder)builder).ConfigureContainer(new GatedServiceProviderFactory(release)));

        var calls = backend.Recording.MethodCalls.Select(c => c.MethodName).ToList();
        var showIndex = calls.IndexOf("Show");
        var pumpIndex = calls.IndexOf("RunEventLoopIteration");
        Assert.True(pumpIndex >= 0, "Build() never pumped the event loop while composing.");
        Assert.True(showIndex < pumpIndex, "The window must be shown before pumping starts.");
    }

    [Fact]
    public void Build_StopsPumping_OnceCompositionCompletes()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend);
        var pumpsAfterBuild = backend.Recording.MethodCalls.Count(c => c.MethodName == "RunEventLoopIteration");

        RunApp(app);

        var pumpsAfterRun = backend.Recording.MethodCalls.Count(c => c.MethodName == "RunEventLoopIteration");
        Assert.Equal(pumpsAfterBuild, pumpsAfterRun);
    }

    [Fact]
    public void Build_Throws_WhenCompositionFails()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        builder.ForceDevServer(false);
        ((IHostApplicationBuilder)builder).ConfigureContainer(new ThrowingServiceProviderFactory());

        Assert.Throws<InvalidOperationException>(() => HermesBlazorAppBuilder.BuildForTest(builder, backend));
    }

    [Fact]
    public void Build_ReplaysAnAttachPageMessageThatArrivedDuringComposition()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };
        var baseUri = HermesWebViewManager.AppBaseUri;
        var attachPage = $"__bwv:[\"AttachPage\",\"{baseUri}\",\"{baseUri}\"]";

        using var app = BuildApp(backend, builder =>
            ((IHostApplicationBuilder)builder).ConfigureContainer(
                new StartupSchemeRegistrationTests.CallbackServiceProviderFactory(() => backend.SimulateWebMessage(attachPage))));

        app.App.RootComponents.Add<ReadyComponent>("#app");
        RunApp(app);

        // Outbound messages cross the manager's channel on a worker and land in the
        // recording backend's pending queue; pump it until the render arrives.
        var rendered = SpinWait.SpinUntil(() =>
        {
            backend.ProcessPending();
            return backend.Recording.WebMessagesSent.Any(m => m.Contains("AttachToDocument"));
        }, TimeSpan.FromSeconds(10));

        Assert.True(rendered, "The replayed AttachPage never led to a render; the buffered message did not reach the manager.");
    }

    private static StartupSchemeRegistrationTests.DisposableApp BuildApp(
        RecordingWindowBackend backend, Action<HermesBlazorAppBuilder>? configure = null)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        builder.ForceDevServer(false);
        builder.UseFileProvider(new TestFileProvider()
            .Add("index.html", HostHtml)
            .Add("_framework/blazor.webview.js", "console.log('boot')"));
        configure?.Invoke(builder);
        return new StartupSchemeRegistrationTests.DisposableApp(HermesBlazorAppBuilder.BuildForTest(builder, backend));
    }

    /// <summary>
    /// Run() installs the Hermes synchronization context on the calling thread and,
    /// with the recording backend, returns immediately. Restore the previous context
    /// afterward so the test's own continuations do not post into the recording
    /// backend's queue.
    /// </summary>
    private static void RunApp(StartupSchemeRegistrationTests.DisposableApp app)
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

    /// <summary>
    /// Holds composition open on the worker until the UI thread's pump releases it,
    /// which is only possible if Build() really pumps while waiting.
    /// </summary>
    private sealed class GatedServiceProviderFactory(ManualResetEventSlim release) : IServiceProviderFactory<IServiceCollection>
    {
        public IServiceCollection CreateBuilder(IServiceCollection services) => services;

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return containerBuilder.BuildServiceProvider();
        }
    }

    private sealed class ThrowingServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
    {
        public IServiceCollection CreateBuilder(IServiceCollection services) => services;

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder) =>
            throw new InvalidOperationException("composition failed");
    }

    private sealed class ReadyComponent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "h1");
            builder.AddContent(1, "READY");
            builder.CloseElement();
        }
    }
}
