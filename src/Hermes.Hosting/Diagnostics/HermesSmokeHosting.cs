// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hermes.Diagnostics;

/// <summary>
/// Wires smoke mode into an app's host and resolves its checks, for every Hermes app type.
/// </summary>
internal static class HermesSmokeHosting
{
    /// <summary>Starts the session and makes it the app's smoke session, with error logs failing the run.</summary>
    public static void Enable(IHostApplicationBuilder builder, SmokeSession session)
    {
        session.Start();
        builder.Services.RemoveAll<IHermesSmokeSession>();
        builder.Services.AddSingleton<IHermesSmokeSession>(new ActiveHermesSmokeSession(session));
        builder.Logging.AddProvider(new SmokeLoggerProvider(session));
    }

    /// <summary>Registers the session that does nothing, for a run that is not a smoke run.</summary>
    public static void UseNoOpSession(IServiceCollection services) =>
        services.TryAddSingleton<IHermesSmokeSession>(NoOpHermesSmokeSession.Instance);

    /// <summary>Makes the session wait for every gate registered with AddHermesSmokeGate.</summary>
    public static void RequireRegisteredGates(IServiceProvider services, SmokeSession session)
    {
        foreach (var gate in services.GetServices<SmokeGateRegistration>())
            session.RequireGate(gate.Name);
    }

    /// <summary>Resolves the registered checks, then the checks each registered source supplies.</summary>
    public static IReadOnlyList<SmokeCheck> ResolveChecks(IServiceProvider scopedServices)
    {
        var checks = new List<SmokeCheck>();
        foreach (var check in scopedServices.GetServices<IHermesSmokeCheck>())
            checks.Add(ToSmokeCheck(check));

        foreach (var source in scopedServices.GetServices<IHermesSmokeCheckSource>())
        {
            foreach (var check in source.GetChecks(scopedServices))
                checks.Add(ToSmokeCheck(check));
        }

        return checks;
    }

    private static SmokeCheck ToSmokeCheck(IHermesSmokeCheck check) => new(check.Name, check.Timeout, check.RunAsync);
}
