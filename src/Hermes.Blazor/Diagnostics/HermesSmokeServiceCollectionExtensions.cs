// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Registers smoke checks and gates. Registration is cheap and always allowed; nothing is created or
/// run unless the process is a smoke run.
/// </summary>
public static class HermesSmokeServiceCollectionExtensions
{
    /// <summary>Registers a smoke check.</summary>
    public static IServiceCollection AddHermesSmokeCheck<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceCollection services)
        where T : class, IHermesSmokeCheck
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IHermesSmokeCheck, T>());
        return services;
    }

    /// <summary>Registers a source of smoke checks, for layers with their own check interface.</summary>
    public static IServiceCollection AddHermesSmokeCheckSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceCollection services)
        where T : class, IHermesSmokeCheckSource
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IHermesSmokeCheckSource, T>());
        return services;
    }

    /// <summary>
    /// Requires a named gate: in smoke mode the checks wait until something calls
    /// <see cref="IHermesSmokeSession.CompleteGate"/> with this name.
    /// </summary>
    public static IServiceCollection AddHermesSmokeGate(this IServiceCollection services, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        services.AddSingleton(new SmokeGateRegistration(name));
        return services;
    }
}
