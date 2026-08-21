// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.Extensions.Hosting;

namespace Hermes.Blazor;

/// <summary>
/// No-op host lifetime. The native window loop owns process lifetime, so the
/// default ConsoleLifetime must not run; its console signal handling would
/// fight the window loop over Ctrl+C and shutdown sequencing.
/// </summary>
internal sealed class WindowHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
