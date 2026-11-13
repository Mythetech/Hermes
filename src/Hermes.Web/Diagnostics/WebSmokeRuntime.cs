// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Hermes.Web.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Web.Diagnostics;

/// <summary>
/// The web side of a smoke run: the page's ready signal is the first render, and page errors fail the run.
/// </summary>
internal sealed class WebSmokeRuntime(SmokeSession session)
{
    private int _started;
    private Task? _verdict;

    internal SmokeSession Session { get; } = session;

    /// <summary>Runs the checks once, off the bridge's reader so checks can talk to the page.</summary>
    internal void OnPageReady(IServiceProvider services)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        // Joined in WaitForVerdict, and RunChecksAsync records its own failures, so this is not fire-and-forget.
        _verdict = Task.Run(() => RunChecksAsync(services));
    }

    internal void OnPageError(PageError error) => Session.RecordError("page", error.Name, error.Message, error.Stack);

    /// <summary>Blocking join for teardown, where an await could not resume on the UI thread.</summary>
    internal void WaitForVerdict(TimeSpan timeout)
    {
        if (_verdict is not null && !_verdict.Wait(timeout))
            HermesLogger.Warning("The smoke checks were still running when the app was disposed.");
    }

    private async Task RunChecksAsync(IServiceProvider services)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            await HermesSmokeHosting.RunChecksAsync(Session, scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            Session.RecordError("hermes", ex);
        }
    }
}
