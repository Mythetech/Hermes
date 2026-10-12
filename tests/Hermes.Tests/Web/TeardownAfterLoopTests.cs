// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Blazor.Threading;
using Hermes.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using Xunit;

namespace Hermes.Tests.Web;

/// <summary>
/// An app's Main is synchronous: Run() returns once the window has closed, and the message loop with it,
/// and Main then blocks on DisposeAsync(). The recording backend is in that state as soon as Run() returns:
/// nothing drains work queued for the UI thread. Each test stays on one thread, as Main does.
/// </summary>
public class TeardownAfterLoopTests
{
    private const string AttachPage = """__bwv:["AttachPage","app://localhost/","app://localhost/"]""";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_Completes_WhenAPageServiceResumesFromAnotherThread(bool fastStartup)
    {
        var teardown = RunThenDispose(
            async _ => await Task.Delay(TimeSpan.FromMilliseconds(20)),
            fastStartup);

        Assert.True(teardown.PageServiceDisposeStarted, "The page service was never disposed.");
        Assert.True(teardown.Completed, "DisposeAsync did not complete after the message loop had ended.");
    }

    [Fact]
    public void Dispose_Completes_WhenAPageServiceUsesTheDispatcherFromAnotherThread()
    {
        var teardown = RunThenDispose(async page =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20));
            await page.Dispatcher.InvokeAsync(() => { });
        });

        Assert.True(teardown.PageServiceDisposeStarted, "The page service was never disposed.");
        Assert.True(teardown.Completed, "DisposeAsync did not complete after the message loop had ended.");
    }

    [Fact]
    public void Dispose_Completes_AndJavaScriptCallsFailAsDisconnected_WhenAPageServiceCallsJavaScript()
    {
        Exception? thrown = null;

        var teardown = RunThenDispose(async page =>
        {
            try
            {
                await page.JavaScript.InvokeVoidAsync("cleanUp");
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });

        Assert.True(teardown.PageServiceDisposeStarted, "The page service was never disposed.");
        Assert.True(teardown.Completed, "DisposeAsync did not complete after the message loop had ended.");
        Assert.IsType<JSDisconnectedException>(thrown);
    }

    [Fact]
    public void Dispose_DisposesTheWindowOnTheUiThread_WhenAPageServiceResumesFromAnotherThread()
    {
        var teardown = RunThenDispose(async _ => await Task.Delay(TimeSpan.FromMilliseconds(20)));

        Assert.True(teardown.Completed, "DisposeAsync did not complete after the message loop had ended.");
        var windowDispose = teardown.Backend.Recording.MethodCalls.First(c => c.MethodName == nameof(IDisposable.Dispose));
        Assert.Equal(teardown.Backend.UIThreadId, windowDispose.ManagedThreadId);
    }

    [Fact]
    public void Dispose_DisposesTheWindowOnTheUiThread_WhenStartupWasStillRunning()
    {
        var teardown = RunThenDispose<PageThatNeverFinishesStarting>(_ => Task.CompletedTask);

        Assert.True(teardown.Completed, "DisposeAsync did not complete after the message loop had ended.");
        var windowDispose = teardown.Backend.Recording.MethodCalls.First(c => c.MethodName == nameof(IDisposable.Dispose));
        Assert.Equal(teardown.Backend.UIThreadId, windowDispose.ManagedThreadId);
    }

    [Fact]
    public void Dispose_StillDisposesTheWindow_WhenAPageServiceNeverFinishes()
    {
        var never = new TaskCompletionSource();

        var teardown = RunThenDispose(
            _ => never.Task,
            configure: builder => builder.Services.Configure<HostOptions>(
                options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(200)));

        Assert.True(teardown.PageServiceDisposeStarted, "The page service was never disposed.");
        Assert.True(teardown.Completed, "DisposeAsync waited forever on a page service that never finished.");
        Assert.Contains(teardown.Backend.Recording.MethodCalls, c => c.MethodName == nameof(IDisposable.Dispose));
    }

    private static Teardown RunThenDispose(
        Func<PageServices, Task> onPageServiceDispose,
        bool fastStartup = false,
        Action<HermesBlazorAppBuilder>? configure = null)
        => RunThenDispose<Page>(onPageServiceDispose, fastStartup, configure);

    private static Teardown RunThenDispose<TPage>(
        Func<PageServices, Task> onPageServiceDispose,
        bool fastStartup = false,
        Action<HermesBlazorAppBuilder>? configure = null)
        where TPage : IComponent
    {
        var previous = SynchronizationContext.Current;
        try
        {
            var probe = new DisposeProbe(onPageServiceDispose);
            var backend = new RecordingWindowBackend();
            var app = CreateApp(backend, builder =>
            {
                builder.Services.AddSingleton(probe);
                builder.Services.AddScoped<PageService>();
                configure?.Invoke(builder);
            });
            app.RootComponents.Add<TPage>("#app");

            backend.SimulateWebMessage(AttachPage);
            if (fastStartup)
                app.RunWithFastStartup();
            else
                app.Run();

            var completed = app.DisposeAsync().AsTask().Wait(Deadline);
            return new Teardown(completed, probe.Started, backend);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static HermesBlazorApp CreateApp(RecordingWindowBackend backend, Action<HermesBlazorAppBuilder> configure)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        configure(builder);
        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        var window = composition.ServiceProvider.GetRequiredService<HermesWindow>();
        var syncContext = composition.ServiceProvider.GetRequiredService<HermesSynchronizationContext>();
        var dispatcher = composition.ServiceProvider.GetRequiredService<HermesDispatcher>();

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
            windowShownDuringBuild: false,
            host: composition.Host);
    }

    private sealed record Teardown(bool Completed, bool PageServiceDisposeStarted, RecordingWindowBackend Backend);

    private sealed record PageServices(IJSRuntime JavaScript, Dispatcher Dispatcher);

    private sealed class DisposeProbe(Func<PageServices, Task> onDispose)
    {
        private volatile bool _started;

        public bool Started => _started;

        public Task RunAsync(PageServices page)
        {
            _started = true;
            return onDispose(page);
        }
    }

    private sealed class PageService(DisposeProbe probe, IJSRuntime javaScript, HermesDispatcher dispatcher) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await probe.RunAsync(new PageServices(javaScript, dispatcher));
    }

    private class Page : ComponentBase
    {
        [Inject]
        public PageService Service { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "page");
    }

    private sealed class PageThatNeverFinishesStarting : Page
    {
        protected override Task OnInitializedAsync() => new TaskCompletionSource().Task;
    }
}
