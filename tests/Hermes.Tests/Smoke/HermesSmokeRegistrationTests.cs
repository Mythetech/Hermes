// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Smoke;

public class HermesSmokeRegistrationTests
{
    [Fact]
    public void AddHermesSmokeCheck_RegistersEachCheckOnce()
    {
        var services = new ServiceCollection();

        services.AddHermesSmokeCheck<OkCheck>().AddHermesSmokeCheck<OkCheck>();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<OkCheck>(Assert.Single(provider.GetServices<IHermesSmokeCheck>()));
    }

    [Fact]
    public void RegisteringACheck_DoesNotCreateIt()
    {
        var services = new ServiceCollection();

        services.AddHermesSmokeCheck<CountingCheck>();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(0, CountingCheck.Created);
    }

    [Fact]
    public void AddHermesSmokeCheckSource_RegistersTheSource()
    {
        var services = new ServiceCollection();

        services.AddHermesSmokeCheckSource<OkSource>();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<OkSource>(Assert.Single(provider.GetServices<IHermesSmokeCheckSource>()));
    }

    [Fact]
    public void AddHermesSmokeGate_RegistersTheGateName()
    {
        var services = new ServiceCollection();

        services.AddHermesSmokeGate("app-ready");

        using var provider = services.BuildServiceProvider();
        Assert.Equal("app-ready", Assert.Single(provider.GetServices<SmokeGateRegistration>()).Name);
    }

    [Fact]
    public void AddHermesSmokeGate_RejectsABlankName()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().AddHermesSmokeGate(" "));
    }

    [Fact]
    public void NoOpSession_IsDisabled_AndIgnoresGates()
    {
        var session = NoOpHermesSmokeSession.Instance;

        session.CompleteGate("app-ready");
        session.FailGate("app-ready", "ignored");

        Assert.False(session.IsEnabled);
    }

    [Fact]
    public async Task ActiveSession_ForwardsGatesToTheRun()
    {
        var harness = new SmokeHarness();
        var run = harness.CreateSession(exitWhenDone: false);
        run.RequireGate("app-ready");
        SmokeHarness.Boot(run);
        var session = new ActiveHermesSmokeSession(run);

        var verdict = run.RunToVerdictAsync([]);
        session.CompleteGate("app-ready");
        await verdict;

        Assert.True(session.IsEnabled);
        Assert.Equal("HERMES_SMOKE_RESULT: PASSED (0 checks)", harness.Lines[^1]);
    }

    private sealed class OkCheck : IHermesSmokeCheck
    {
        public string Name => "test/ok";

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CountingCheck : IHermesSmokeCheck
    {
        public static int Created;

        public CountingCheck() => Interlocked.Increment(ref Created);

        public string Name => "test/counting";

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class OkSource : IHermesSmokeCheckSource
    {
        public IEnumerable<IHermesSmokeCheck> GetChecks(IServiceProvider scopedServices) => [new OkCheck()];
    }
}
