// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Web.Interop;
using Microsoft.Extensions.Hosting;

namespace SvelteHelloWorld;

/// <summary>
/// Sends the page a "tick" event with the seconds since the window opened.
/// </summary>
internal sealed class TickService(InteropBridge bridge) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var seconds = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                bridge.Send("tick", ++seconds);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
