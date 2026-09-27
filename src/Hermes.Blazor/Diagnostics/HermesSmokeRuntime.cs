// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics.Smoke;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// The Blazor side of a smoke run: counts wrapped roots, marks the first render once all have rendered,
/// and runs the checks in the page's service scope.
/// </summary>
internal sealed class HermesSmokeRuntime(SmokeSession session)
{
    private int _expectedRoots = 1;
    private int _renderedRoots;
    private int _verdictStarted;
    private IServiceProvider? _firstScope;

    internal SmokeSession Session { get; } = session;

    internal int ExpectedRootCount => _expectedRoots;

    internal void SetExpectedRootCount(int count) => _expectedRoots = count;

    /// <summary>
    /// Called by each wrapped root after its first render. Never throws: a failure here is recorded and
    /// still ends in a verdict.
    /// </summary>
    internal async Task OnRootRenderedAsync(IServiceProvider scopedServices)
    {
        Interlocked.CompareExchange(ref _firstScope, scopedServices, null);
        if (Interlocked.Increment(ref _renderedRoots) < _expectedRoots)
            return;
        if (Interlocked.Exchange(ref _verdictStarted, 1) == 1)
            return;

        Session.MarkMilestone(SmokeSession.FirstRenderMilestone);

        IReadOnlyList<SmokeCheck> checks;
        try
        {
            checks = ResolveChecks(_firstScope!);
        }
        catch (Exception ex)
        {
            Session.RecordError("hermes", ex);
            checks = [];
        }

        await Session.RunToVerdictAsync(checks);
    }

    internal static IReadOnlyList<SmokeCheck> ResolveChecks(IServiceProvider scopedServices)
    {
        var checks = new List<SmokeCheck>();
        foreach (var check in scopedServices.GetServices<IHermesSmokeCheck>())
            checks.Add(ToSmokeCheck(check));

        foreach (var source in scopedServices.GetServices<IHermesSmokeCheckSource>())
        {
            foreach (var check in source.GetChecks(scopedServices))
                checks.Add(ToSmokeCheck(check));
        }

        return checks;
    }

    private static SmokeCheck ToSmokeCheck(IHermesSmokeCheck check) => new(check.Name, check.Timeout, check.RunAsync);
}
