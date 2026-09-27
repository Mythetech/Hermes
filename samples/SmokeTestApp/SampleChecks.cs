// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.Diagnostics;

namespace SmokeTestApp;

public sealed class PassingCheck : IHermesSmokeCheck
{
    public string Name => "smoke-sample/passing";

    public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FailingCheck : IHermesSmokeCheck
{
    public string Name => "smoke-sample/failing";

    public Task RunAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Deliberate smoke sample check failure");
}
