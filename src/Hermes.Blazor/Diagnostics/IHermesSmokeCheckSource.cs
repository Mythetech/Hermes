// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Supplies smoke checks resolved some other way, for layers that wrap Hermes behind their own check
/// interface. Called once in smoke mode with the page's service scope.
/// </summary>
public interface IHermesSmokeCheckSource
{
    /// <summary>Returns the checks to run, resolved from <paramref name="scopedServices"/>.</summary>
    IEnumerable<IHermesSmokeCheck> GetChecks(IServiceProvider scopedServices);
}
