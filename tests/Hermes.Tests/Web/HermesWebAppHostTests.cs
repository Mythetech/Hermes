// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Collections.Concurrent;
using Hermes.Contracts.Hotkeys;
using Hermes.Contracts.Notifications;
using Hermes.Contracts.Plugins;
using Hermes.Diagnostics;
using Hermes.Testing;
using Hermes.Web;
using Hermes.Web.Interop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class HermesWebAppHostTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Services_ResolveWindowBridgeAndPlatformServices()
    {
        var (app, _) = Build();

        Assert.Same(app.MainWindow, app.Services.GetRequiredService<HermesWindow>());
        Assert.Same(app.Bridge, app.Services.GetRequiredService<InteropBridge>());
        Assert.NotNull(app.Services.GetRequiredService<IConfiguration>());
        Assert.NotNull(app.Services.GetRequiredService<IClipboard>());
        Assert.NotNull(app.Services.GetRequiredService<IDragDrop>());
        Assert.NotNull(app.Services.GetRequiredService<IGlobalHotkeys>());
        Assert.NotNull(app.Services.GetRequiredService<INativeNotifications>());
        Assert.NotNull(app.Services.GetRequiredService<IHostApplicationLifetime>());
        Assert.False(app.Services.GetRequiredService<IHermesSmokeSession>().IsEnabled);

        await app.DisposeAsync();
    }

    [Fact]
    public async Task HostedService_StartsAfterRun_AndStopsOnDispose_BeforeProviderDisposal()
    {
        var log = new ConcurrentQueue<string>();
        var service = new RecordingService(log);
        var (app, _) = Build(builder =>
        {
            builder.Services.AddSingleton<IHostedService>(service);
            builder.Services.AddSingleton(_ => new DisposalProbe(log));
        });
        _ = app.Services.GetRequiredService<DisposalProbe>();

        Assert.False(service.Started.Task.IsCompleted);
        WebAppRunner.Run(app);
        await service.Started.Task.WaitAsync(Wait);
        await app.DisposeAsync();

        Assert.Equal(["start", "stop", "provider-disposed"], log);
    }

    [Fact]
    public async Task DisposeAsync_StopsTheBridgeBeforeHostedServices()
    {
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken handlerToken = default;
        var cancelledWhenStopping = (bool?)null;
        var probe = new StopProbe(() => cancelledWhenStopping = handlerToken.IsCancellationRequested);
        var (app, backend) = Build(builder => builder.Services.AddSingleton<IHostedService>(probe));
        app.Bridge.HandleAsync("wait", async (CancellationToken token) =>
        {
            handlerToken = token;
            handlerEntered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });

        WebAppRunner.Run(app);
        backend.SimulateWebMessage("""{"type":"invoke","id":"1","method":"wait","args":[]}""");
        await handlerEntered.Task.WaitAsync(Wait);
        await probe.Started.Task.WaitAsync(Wait);
        await app.DisposeAsync();

        Assert.True(cancelledWhenStopping);
    }

    [Fact]
    public async Task DisposeAsync_StopsHostedServicesBeforeTheWindow_AndDisposesItOnTheCallingThread()
    {
        RecordingWindowBackend? backend = null;
        var windowDisposedWhenStopping = (bool?)null;
        var probe = new StopProbe(async () =>
        {
            await Task.Delay(50);
            windowDisposedWhenStopping = backend!.Recording.MethodWasCalled("Dispose");
        });
        var (app, recording) = Build(builder => builder.Services.AddSingleton<IHostedService>(probe));
        backend = recording;

        WebAppRunner.Run(app);
        await probe.Started.Task.WaitAsync(Wait);
        var thread = Environment.CurrentManagedThreadId;
        await app.DisposeAsync().AsTask().WaitAsync(Wait);

        Assert.False(windowDisposedWhenStopping);
        Assert.Equal(thread, backend.Recording.MethodCalls.Single(c => c.MethodName == "Dispose").ManagedThreadId);
    }

    [Fact]
    public async Task Run_ShowsBeforeHostedServicesStart()
    {
        RecordingWindowBackend? backend = null;
        var shownWhenStarting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (app, recording) = Build(builder =>
            builder.Services.AddSingleton<IHostedService>(new StartProbe(() => shownWhenStarting.TrySetResult(backend!.IsShown))));
        backend = recording;

        WebAppRunner.Run(app);

        Assert.True(await shownWhenStarting.Task.WaitAsync(Wait));
        await app.DisposeAsync();
    }

    private static (HermesWebApp App, RecordingWindowBackend Backend) Build(Action<HermesWebAppBuilder>? configure = null)
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesWebAppBuilder.CreateSlimBuilder(["--environment", "Production"]);
        configure?.Invoke(builder);
        return (HermesWebAppBuilder.BuildForTest(builder, backend), backend);
    }

    private sealed class RecordingService(ConcurrentQueue<string> log) : IHostedService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            log.Enqueue("start");
            Started.TrySetResult();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            log.Enqueue("stop");
            return Task.CompletedTask;
        }
    }

    private sealed class DisposalProbe(ConcurrentQueue<string> log) : IDisposable
    {
        public void Dispose() => log.Enqueue("provider-disposed");
    }

    private sealed class StopProbe : IHostedService
    {
        private readonly Func<Task> _onStop;

        public StopProbe(Action onStop) : this(() => { onStop(); return Task.CompletedTask; })
        {
        }

        public StopProbe(Func<Task> onStop) => _onStop = onStop;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => _onStop();
    }

    private sealed class StartProbe(Action onStart) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            onStart();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
