// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Hosting;

/// <summary>
/// Host configuration shared by every Hermes app type.
/// </summary>
internal static class HermesHostDefaults
{
    /// <summary>Creates an empty host builder whose environment comes from --environment or DOTNET_ENVIRONMENT.</summary>
    public static HostApplicationBuilder CreateBuilder(string[]? args) =>
        Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            DisableDefaults = true,
            EnvironmentName = ResolveEnvironmentName(args, Environment.GetEnvironmentVariable)
        });

    /// <summary>The command line's environment, else DOTNET_ENVIRONMENT; null leaves the host's Production default.</summary>
    public static string? ResolveEnvironmentName(string[]? args, Func<string, string?> getEnvironmentVariable)
    {
        var commandLine = new ConfigurationBuilder().AddCommandLine(args ?? []).Build();
        var name = commandLine[HostDefaults.EnvironmentKey];
        if (string.IsNullOrEmpty(name))
            name = getEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>Adds appsettings.json, the environment's appsettings file, environment variables and the command line.</summary>
    public static void AddDefaultConfiguration(HostApplicationBuilder builder, string[]? args)
    {
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args ?? []);
    }

    /// <summary>Lets the native window loop own the process lifetime and tunes hosted service start and stop for a desktop app.</summary>
    public static void AddWindowHost(IServiceCollection services)
    {
        // The window loop owns process lifetime; the default ConsoleLifetime
        // would install console signal handling that fights the native loop.
        services.AddSingleton<IHostLifetime, WindowHostLifetime>();

        // Hosted services start after first paint, so one slow StartAsync must
        // not serialize the rest, and window close should not hang on a long
        // shutdown. Both stay user-overridable via Configure<HostOptions>.
        services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = true;
            options.ShutdownTimeout = HermesHostRunner.DefaultShutdownTimeout;
        });
    }
}
