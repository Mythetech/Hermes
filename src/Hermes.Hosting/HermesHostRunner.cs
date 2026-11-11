// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ApplicationLifetime = Microsoft.Extensions.Hosting.Internal.ApplicationLifetime;

namespace Hermes.Hosting;

/// <summary>
/// Starts an app's hosted services once its window is up and stops them after the message loop has ended.
/// </summary>
internal sealed class HermesHostRunner : IAsyncDisposable
{
    internal static TimeSpan DefaultShutdownTimeout { get; } = TimeSpan.FromSeconds(5);

    private readonly IHost _host;
    private readonly HermesWindow _window;
    private readonly IHostApplicationLifetime? _applicationLifetime;
    private readonly CancellationTokenSource _startCancellation = new();
    private Task? _startTask;

    public HermesHostRunner(IHost host, HermesWindow window)
    {
        _host = host;
        _window = window;
        _applicationLifetime = host.Services.GetService<IHostApplicationLifetime>();

        if (_applicationLifetime is not null)
        {
            // ApplicationStopping ties to the beginning of window close, not to
            // disposal, so services can react while the window still exists.
            _window.Backend.Closing += NotifyApplicationStopping;
        }
    }

    public TimeSpan ShutdownTimeout =>
        _host.Services.GetService<IOptions<HostOptions>>()?.Value.ShutdownTimeout ?? DefaultShutdownTimeout;

    public void BeginStart()
    {
        if (_startTask is not null)
            return;

        // Hosted services start on the thread pool so a slow StartAsync can
        // never delay first paint or the message loop. The task observes its
        // own exceptions and is joined in Stop, so it is not fire-and-forget.
        _startTask = Task.Run(() => StartAsync(_startCancellation.Token));
    }

    public void Stop()
    {
        if (_applicationLifetime is not null)
            _window.Backend.Closing -= NotifyApplicationStopping;

        _startCancellation.Cancel();

        // Blocking joins, deliberately: the message loop has already exited by
        // the time the app is disposed, so await continuations posted through
        // the synchronization context would land in a queue nothing drains.
        // The start task runs entirely on the thread pool and observes its own
        // exceptions, so these waits complete without the pump.
        if (_startTask is not null && !_startTask.Wait(ShutdownTimeout))
        {
            HermesLogger.Error(
                "Hosted services did not finish starting within the shutdown timeout; continuing teardown.");
        }

        try
        {
            _host.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Hosted services failed to stop cleanly: {ex}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Disposing the host also disposes the service provider it built.
        if (_host is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _host.Dispose();

        _startCancellation.Dispose();
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The window is already visible, which is the honest "started"
            // signal for a desktop app. Fire ApplicationStarted now instead of
            // after every hosted service finishes starting; the host's own
            // NotifyStarted call later is an idempotent no-op.
            if (_applicationLifetime is ApplicationLifetime applicationLifetime)
                applicationLifetime.NotifyStarted();

            await _host.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown began before hosted services finished starting.
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Hosted services failed to start: {ex}");
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private void NotifyApplicationStopping() => _applicationLifetime!.StopApplication();
}
