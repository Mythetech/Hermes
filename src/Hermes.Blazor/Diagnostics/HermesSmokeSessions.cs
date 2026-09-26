// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics.Smoke;

namespace Hermes.Blazor.Diagnostics;

internal sealed class NoOpHermesSmokeSession : IHermesSmokeSession
{
    internal static NoOpHermesSmokeSession Instance { get; } = new();

    public bool IsEnabled => false;

    public void CompleteGate(string name)
    {
    }

    public void FailGate(string name, string reason)
    {
    }
}

internal sealed class ActiveHermesSmokeSession(SmokeSession session) : IHermesSmokeSession
{
    public bool IsEnabled => true;

    public void CompleteGate(string name) => session.CompleteGate(name);

    public void FailGate(string name, string reason) => session.FailGate(name, reason);
}

internal sealed record SmokeGateRegistration(string Name);
