// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// The running smoke session. Always resolvable; when smoke mode is off every member does nothing,
/// so callers never need to check first.
/// </summary>
public interface IHermesSmokeSession
{
    /// <summary>Whether this process is a smoke run.</summary>
    bool IsEnabled { get; }

    /// <summary>Marks a gate registered with AddHermesSmokeGate as reached, letting the checks run.</summary>
    void CompleteGate(string name);

    /// <summary>Marks a gate as reached but failed; the run continues and then fails.</summary>
    void FailGate(string name, string reason);
}
