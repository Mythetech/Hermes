// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Hermes.Hosting;
using Hermes.Tests.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hermes.Tests.Hosting;

/// <summary>
/// Smoke mode registration on a plain host with no Blazor in it.
/// </summary>
public class HermesSmokeHostingTests
{
    [Fact]
    public void Enable_RegistersTheActiveSessionAndTheSmokeLogger()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        HermesSmokeHosting.Enable(builder, new SmokeHarness().CreateSession(exitWhenDone: false));

        using var host = builder.Build();
        Assert.True(host.Services.GetRequiredService<IHermesSmokeSession>().IsEnabled);
        Assert.Contains(host.Services.GetServices<ILoggerProvider>(), p => p is SmokeLoggerProvider);
    }

    [Fact]
    public void UseNoOpSession_RegistersTheDisabledSession()
    {
        var services = new ServiceCollection();

        HermesSmokeHosting.UseNoOpSession(services);

        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<IHermesSmokeSession>().IsEnabled);
    }

    [Fact]
    public void ResolveChecks_ReturnsDirectChecksThenSourceChecks()
    {
        var services = new ServiceCollection();
        services.AddHermesSmokeCheckSource<OneCheckSource>();
        services.AddHermesSmokeCheck<DirectCheck>();
        using var provider = services.BuildServiceProvider();

        var names = HermesSmokeHosting.ResolveChecks(provider).Select(c => c.Name);

        Assert.Equal(["direct", "from-source"], names);
    }

    [Fact]
    public async Task RunChecksAsync_MarksFirstRender_RunsChecks_AndPasses()
    {
        var harness = new SmokeHarness();
        var session = harness.CreateSession(exitWhenDone: false);
        var services = new ServiceCollection();
        services.AddHermesSmokeCheck<DirectCheck>();
        using var provider = services.BuildServiceProvider();
        session.MarkMilestone(SmokeSession.WindowShownMilestone);

        await HermesSmokeHosting.RunChecksAsync(session, provider);

        Assert.Contains(harness.Lines, l => l.StartsWith("HERMES_SMOKE_MILESTONE: first-render", StringComparison.Ordinal));
        Assert.Contains("HERMES_SMOKE_CHECK_PASS: direct", harness.Output.ToString());
        Assert.Contains("HERMES_SMOKE_RESULT: PASSED (1 checks)", harness.Lines);
    }

    [Fact]
    public async Task RunChecksAsync_CheckResolutionFails_RecordsAnError()
    {
        var harness = new SmokeHarness();
        var session = harness.CreateSession(exitWhenDone: false);
        var services = new ServiceCollection();
        services.AddHermesSmokeCheck<ThrowingCheck>();
        using var provider = services.BuildServiceProvider();

        await HermesSmokeHosting.RunChecksAsync(session, provider);

        Assert.Contains(harness.Lines, l => l.StartsWith("HERMES_SMOKE_ERROR: hermes:", StringComparison.Ordinal));
        Assert.Contains(harness.Lines, l => l.StartsWith("HERMES_SMOKE_RESULT: FAILED", StringComparison.Ordinal));
    }

    private sealed class ThrowingCheck : IHermesSmokeCheck
    {
        public ThrowingCheck() => throw new InvalidOperationException("cannot build");

        public string Name => "throwing";

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DirectCheck : IHermesSmokeCheck
    {
        public string Name => "direct";

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SourceCheck : IHermesSmokeCheck
    {
        public string Name => "from-source";

        public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class OneCheckSource : IHermesSmokeCheckSource
    {
        public IEnumerable<IHermesSmokeCheck> GetChecks(IServiceProvider scopedServices) => [new SourceCheck()];
    }
}
