// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Hosting;
using Hermes.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hermes.Tests.Hosting;

/// <summary>
/// The shared runner on its own, without a Blazor or web app around it.
/// </summary>
public sealed class HermesHostRunnerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task BeginStart_StartsHostedServices()
    {
        var service = new RecordingService();
        var (runner, _, _) = Create(service);

        runner.BeginStart();

        await service.Started.Task.WaitAsync(Wait);
        runner.Stop();
        await runner.DisposeAsync();
    }

    [Fact]
    public async Task Stop_StopsThem_AndClosingSignalsApplicationStopping()
    {
        var service = new RecordingService();
        var (runner, host, backend) = Create(service);
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() => stopping.TrySetResult());
        runner.BeginStart();
        await service.Started.Task.WaitAsync(Wait);

        backend.Close();
        await stopping.Task.WaitAsync(Wait);
        runner.Stop();

        Assert.True(service.Stopped);
        await runner.DisposeAsync();
    }

    private static (HermesHostRunner Runner, IHost Host, RecordingWindowBackend Backend) Create(IHostedService service)
    {
        var builder = HermesHostDefaults.CreateBuilder([]);
        HermesHostDefaults.AddWindowHost(builder.Services);
        builder.Services.AddSingleton(service);
        var host = builder.Build();
        var backend = new RecordingWindowBackend();
        return (new HermesHostRunner(host, new HermesWindow(backend)), host, backend);
    }

    private sealed class RecordingService : IHostedService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }
}
