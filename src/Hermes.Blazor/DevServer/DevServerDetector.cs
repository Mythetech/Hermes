// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Blazor.DevServer;

/// <summary>
/// Detects whether the app should use the internal dev server for hot reload.
/// </summary>
internal static class DevServerDetector
{
    /// <summary>
    /// Determines whether the dev server should be used.
    /// </summary>
    /// <param name="forceDevServer">Explicit override from ForceDevServer(). Null means auto-detect.</param>
    /// <param name="getEnvVar">Environment variable reader, injectable for testing.</param>
    internal static bool ShouldUseDevServer(bool? forceDevServer, Func<string, string?> getEnvVar)
    {
        if (forceDevServer.HasValue)
            return forceDevServer.Value;

        return getEnvVar("DOTNET_WATCH") == "1";
    }

    /// <summary>
    /// Production entry point using real environment variables.
    /// </summary>
    internal static bool ShouldUseDevServer(bool? forceDevServer)
        => ShouldUseDevServer(forceDevServer, Environment.GetEnvironmentVariable);
}
