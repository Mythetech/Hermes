// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// A named check that runs once in smoke mode, after the first render and every smoke gate.
/// Checks are resolved from the page's service scope, so they see the same scoped services as the UI,
/// and they are never created outside smoke mode.
/// </summary>
public interface IHermesSmokeCheck
{
    /// <summary>Name shown in the verdict, prefixed by layer, for example "horizon/workspace-store".</summary>
    string Name { get; }

    /// <summary>How long the check may run before it fails. Defaults to 10 seconds.</summary>
    TimeSpan Timeout => TimeSpan.FromSeconds(10);

    /// <summary>Runs the check. Throw to fail it.</summary>
    Task RunAsync(CancellationToken cancellationToken);
}
