// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
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
