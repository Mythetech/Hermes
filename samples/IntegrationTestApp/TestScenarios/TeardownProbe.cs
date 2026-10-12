// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.Extensions.Hosting;

namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// A hosted service whose only job is to prove the app was torn down after the window closed.
/// The code after <c>app.Run()</c> is what a platform skips when it ends the process from inside
/// the message loop, so the verdict is printed from there and CI requires the passing marker.
/// </summary>
public sealed class TeardownProbe : IHostedService
{
    private static volatile bool _stopped;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopped = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Call after the app has been disposed. Emits the HERMES_TEARDOWN marker and fails the exit
    /// code when hosted services were never stopped.
    /// </summary>
    public static void Report()
    {
        if (_stopped)
        {
            Console.WriteLine("HERMES_TEARDOWN: PASSED");
            return;
        }

        Console.WriteLine("HERMES_TEARDOWN: FAILED (hosted services were not stopped after the window closed)");
        Environment.ExitCode = 1;
    }
}
