// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.SingleInstance;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Blazor;

/// <summary>
/// Extension methods for adding single-instance support to Hermes Blazor applications.
/// </summary>
public static class SingleInstanceExtensions
{
    /// <summary>
    /// Enables single-instance mode for this application. If another instance is already
    /// running, this method forwards the current process's command-line arguments to it
    /// and exits the process immediately.
    /// </summary>
    /// <param name="builder">The Blazor app builder.</param>
    /// <param name="applicationId">
    /// A unique identifier for this application. Must contain only alphanumeric characters,
    /// hyphens, underscores, and dots.
    /// </param>
    /// <param name="configure">
    /// Optional callback to configure the guard (e.g., subscribe to
    /// <see cref="SingleInstanceGuard.SecondInstanceLaunched"/>).
    /// Only called on the first (primary) instance.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static HermesBlazorAppBuilder SingleInstance(
        this HermesBlazorAppBuilder builder,
        string applicationId,
        Action<SingleInstanceGuard>? configure = null)
    {
        var guard = new SingleInstanceGuard(applicationId);

        if (!guard.IsFirstInstance)
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            guard.NotifyFirstInstance(args);
            guard.Dispose();
            Environment.Exit(0);
        }

        configure?.Invoke(guard);
        builder.Services.AddSingleton(guard);
        return builder;
    }
}
