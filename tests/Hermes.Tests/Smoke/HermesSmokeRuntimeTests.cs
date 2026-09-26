// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Smoke;

public class HermesSmokeRuntimeTests
{
    private readonly SmokeHarness _harness = new();

    private HermesSmokeRuntime CreateRuntime(int roots = 1)
    {
        var session = _harness.CreateSession(exitWhenDone: false);
        session.MarkMilestone(SmokeSession.WindowShownMilestone);
        var runtime = new HermesSmokeRuntime(session);
        runtime.SetExpectedRootCount(roots);
        return runtime;
    }

    [Fact]
    public async Task FirstRender_IsMarkedOnlyAfterEveryRootRendered()
    {
        var runtime = CreateRuntime(roots: 2);
        using var provider = new ServiceCollection().BuildServiceProvider();

        await runtime.OnRootRenderedAsync(provider);
        Assert.DoesNotContain(_harness.Lines, line => line.StartsWith("HERMES_SMOKE_MILESTONE: first-render", StringComparison.Ordinal));

        await runtime.OnRootRenderedAsync(provider);
        Assert.Contains("HERMES_SMOKE_MILESTONE: first-render 0ms", _harness.Lines);
        Assert.Equal("HERMES_SMOKE_RESULT: PASSED (0 checks)", _harness.Lines[^1]);
    }

    [Fact]
    public async Task Checks_AreResolvedFromTheScope_IncludingSources_InOrder()
    {
        var runtime = CreateRuntime();
        var services = new ServiceCollection();
        services.AddHermesSmokeCheck<DirectCheck>();
        services.AddHermesSmokeCheckSource<TwoCheckSource>();
        using var provider = services.BuildServiceProvider();

        await runtime.OnRootRenderedAsync(provider);

        var checkLines = _harness.Lines.Where(line => line.StartsWith("HERMES_SMOKE_CHECK_PASS:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(
            new[]
            {
                "HERMES_SMOKE_CHECK_PASS: test/direct 0ms",
                "HERMES_SMOKE_CHECK_PASS: test/source-a 0ms",
                "HERMES_SMOKE_CHECK_PASS: test/source-b 0ms",
            },
            checkLines);
    }

    [Fact]
    public async Task ACheckThatCannotBeCreated_IsRecorded_AndStillEndsInAVerdict()
    {
        var runtime = CreateRuntime();
        var services = new ServiceCollection();
        services.AddHermesSmokeCheck<CheckWithMissingDependency>();
        using var provider = services.BuildServiceProvider();

        await runtime.OnRootRenderedAsync(provider);

        Assert.Contains(_harness.Lines, line => line.StartsWith("HERMES_SMOKE_ERROR: hermes: InvalidOperationException:", StringComparison.Ordinal));
        Assert.StartsWith("HERMES_SMOKE_RESULT: FAILED", _harness.Lines[^1]);
    }

    private sealed class DirectCheck : IHermesSmokeCheck
    {
        public string Name => "test/direct";

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NamedCheck(string name) : IHermesSmokeCheck
    {
        public string Name => name;

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TwoCheckSource : IHermesSmokeCheckSource
    {
        public IEnumerable<IHermesSmokeCheck> GetChecks(IServiceProvider scopedServices) =>
            [new NamedCheck("test/source-a"), new NamedCheck("test/source-b")];
    }

    private interface IUnregistered
    {
    }

    private sealed class CheckWithMissingDependency(IUnregistered dependency) : IHermesSmokeCheck
    {
        public string Name => $"test/{dependency}";

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
