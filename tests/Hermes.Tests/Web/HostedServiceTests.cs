// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using Hermes.Blazor;
using Hermes.Blazor.Threading;
using Hermes.Testing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hermes.Tests.Web;

public class HostedServiceTests
{
    [Fact]
    public void ComposeServices_BuildsRealHost()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        Assert.NotNull(composition.Host);
        Assert.Same(composition.Host.Services, composition.ServiceProvider);
    }

    [Fact]
    public void ComposeServices_ReplacesConsoleLifetimeWithWindowLifetime()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        Assert.IsType<WindowHostLifetime>(composition.Host.Services.GetRequiredService<IHostLifetime>());
    }

    [Fact]
    public void ComposeServices_ConfiguresConcurrentStartAndShutdownTimeout()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        var options = composition.Host.Services.GetRequiredService<IOptions<HostOptions>>().Value;
        Assert.True(options.ServicesStartConcurrently);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
    }

    [Fact]
    public async Task HostedService_StartAsyncIsCalled_AfterRun()
    {
        var service = new RecordingHostedService();
        var (app, _) = CreateApp(builder => builder.Services.AddSingleton<IHostedService>(service));

        RunApp(app);

        await service.StartCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await app.DisposeAsync();
    }

    [Fact]
    public async Task HostedService_StopAsyncIsCalled_OnDispose_BeforeProviderDisposal()
    {
        var log = new OrderLog();
        var service = new RecordingHostedService(log);
        var (app, _) = CreateApp(builder =>
        {
            builder.Services.AddSingleton<IHostedService>(service);
            builder.Services.AddSingleton(_ => new DisposalProbe(log));
        });

        RunApp(app);
        _ = app.Services.GetRequiredService<DisposalProbe>();
        await service.StartCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await app.DisposeAsync();

        var entries = log.Entries;
        var stopIndex = entries.IndexOf("stop");
        var disposeIndex = entries.IndexOf("provider-disposed");
        Assert.True(stopIndex >= 0, "StopAsync was never called on the hosted service.");
        Assert.True(disposeIndex >= 0, "The service provider was never disposed.");
        Assert.True(stopIndex < disposeIndex, "StopAsync must run before the provider is disposed.");
    }

    [Fact]
    public async Task Run_IsNotDelayed_ByASlowHostedServiceStart()
    {
        var (app, backend) = CreateApp(builder =>
            builder.Services.AddSingleton<IHostedService>(new SlowStartService(TimeSpan.FromSeconds(10))));

        var stopwatch = Stopwatch.StartNew();
        RunApp(app);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Run took {stopwatch.Elapsed}; a slow hosted service must not hold up the show path.");
        Assert.Contains(backend.Recording.MethodCalls, c => c.MethodName == "Show");

        await app.DisposeAsync();
    }

    [Fact]
    public async Task ApplicationStarted_FiresAtFirstPaint_NotAfterSlowServiceStart()
    {
        var (app, _) = CreateApp(builder =>
            builder.Services.AddSingleton<IHostedService>(new SlowStartService(TimeSpan.FromSeconds(10))));

        RunApp(app);

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lifetime.ApplicationStarted.Register(() => started.TrySetResult());

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await app.DisposeAsync();
    }

    [Fact]
    public async Task HostedServices_StartConcurrently()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new HandshakeService(firstEntered, waitFor: secondEntered);
        var second = new HandshakeService(secondEntered, waitFor: firstEntered);
        var (app, _) = CreateApp(builder =>
        {
            builder.Services.AddSingleton<IHostedService>(first);
            builder.Services.AddSingleton<IHostedService>(second);
        });

        RunApp(app);

        await Task.WhenAll(first.Completed.Task, second.Completed.Task).WaitAsync(TimeSpan.FromSeconds(10));

        await app.DisposeAsync();
    }

    [Fact]
    public async Task HostedServiceStartFailure_SurfacesViaDispatcherUnhandledException_AndWindowStillShows()
    {
        var surfaced = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Exception> handler = ex =>
        {
            if (ContainsBoom(ex))
                surfaced.TrySetResult(ex);
        };
        HermesApplication.DispatcherUnhandledException += handler;
        try
        {
            var healthy = new RecordingHostedService();
            var (app, backend) = CreateApp(builder =>
            {
                builder.Services.AddSingleton<IHostedService>(new ThrowingStartService());
                builder.Services.AddSingleton<IHostedService>(healthy);
            });

            RunApp(app);

            await surfaced.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(backend.Recording.MethodCalls, c => c.MethodName == "Show");
            await healthy.StartCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await app.DisposeAsync();
        }
        finally
        {
            HermesApplication.DispatcherUnhandledException -= handler;
        }
    }

    [Fact]
    public async Task ApplicationStoppingAndStopped_FireInOrder_OnCloseAndDispose()
    {
        var service = new RecordingHostedService();
        var (app, _) = CreateApp(builder => builder.Services.AddSingleton<IHostedService>(service));

        RunApp(app);
        await service.StartCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var log = new OrderLog();
        lifetime.ApplicationStopping.Register(() => log.Add("stopping"));
        lifetime.ApplicationStopped.Register(() => log.Add("stopped"));

        app.MainWindow.Close();
        Assert.Equal(new[] { "stopping" }, log.Entries);

        await app.DisposeAsync();
        Assert.Equal(new[] { "stopping", "stopped" }, log.Entries);
    }

    [Fact]
    public async Task HostedService_StartAsyncIsCalled_AfterRunWithFastStartup()
    {
        var service = new RecordingHostedService();
        var (app, _) = CreateApp(builder => builder.Services.AddSingleton<IHostedService>(service));

        var previous = SynchronizationContext.Current;
        try
        {
            app.RunWithFastStartup();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await service.StartCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await app.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_Completes_WhenRunWasNeverCalled()
    {
        var service = new RecordingHostedService();
        var (app, _) = CreateApp(builder => builder.Services.AddSingleton<IHostedService>(service));

        await app.DisposeAsync();
    }

    private static (HermesBlazorApp App, RecordingWindowBackend Backend) CreateApp(
        Action<HermesBlazorAppBuilder>? configure = null)
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        configure?.Invoke(builder);
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

        var app = new HermesBlazorApp(
            composition.ServiceProvider,
            builder.Configuration,
            window,
            webViewManager,
            syncContext,
            windowShownDuringBuild: false,
            host: composition.Host);

        return (app, backend);
    }

    /// <summary>
    /// Run() installs the Hermes synchronization context on the calling thread and, with the
    /// recording backend, returns immediately. Restore the previous context afterward so the
    /// test's own await continuations do not post into the recording backend's queue, which
    /// nothing pumps.
    /// </summary>
    private static void RunApp(HermesBlazorApp app)
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

    private static bool ContainsBoom(Exception exception)
    {
        if (exception is AggregateException aggregate)
            return aggregate.Flatten().InnerExceptions.Any(ContainsBoom);

        if (exception.Message == "boom")
            return true;

        return exception.InnerException is not null && ContainsBoom(exception.InnerException);
    }

    private sealed class OrderLog
    {
        private readonly object _lock = new();
        private readonly List<string> _entries = new();

        public void Add(string entry)
        {
            lock (_lock)
            {
                _entries.Add(entry);
            }
        }

        public List<string> Entries
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_entries);
                }
            }
        }
    }

    private sealed class RecordingHostedService : IHostedService
    {
        private readonly OrderLog? _log;

        public RecordingHostedService(OrderLog? log = null) => _log = log;

        public TaskCompletionSource StartCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _log?.Add("start");
            StartCalled.TrySetResult();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _log?.Add("stop");
            StopCalled.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class SlowStartService : IHostedService
    {
        private readonly TimeSpan _delay;

        public SlowStartService(TimeSpan delay) => _delay = delay;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class HandshakeService : IHostedService
    {
        private readonly TaskCompletionSource _entered;
        private readonly TaskCompletionSource _waitFor;

        public HandshakeService(TaskCompletionSource entered, TaskCompletionSource waitFor)
        {
            _entered = entered;
            _waitFor = waitFor;
        }

        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _waitFor.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            Completed.TrySetResult();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingStartService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DisposalProbe : IDisposable
    {
        private readonly OrderLog _log;

        public DisposalProbe(OrderLog log) => _log = log;

        public void Dispose() => _log.Add("provider-disposed");
    }
}
