// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Supplies smoke checks resolved some other way, for layers that wrap Hermes behind their own check
/// interface. Called once in smoke mode with the page's service scope.
/// </summary>
/// <remarks>
/// <see cref="GetChecks"/> runs at the first render, before the smoke gates are awaited; only the returned
/// checks' <see cref="IHermesSmokeCheck.RunAsync"/> waits for the gates. Anything a gate stands for, such
/// as app startup, may not have finished yet, so resolve and construct checks without depending on it and
/// do that work in <see cref="IHermesSmokeCheck.RunAsync"/> instead.
/// </remarks>
public interface IHermesSmokeCheckSource
{
    /// <summary>Returns the checks to run, resolved from <paramref name="scopedServices"/>.</summary>
    IEnumerable<IHermesSmokeCheck> GetChecks(IServiceProvider scopedServices);
}
