// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Microsoft.Extensions.Configuration;

namespace Hermes.Blazor.Licensing;

internal static class LicenseKeyResolver
{
    private const string EnvVarName = "HERMES_LICENSE";
    private const string ConfigKey = "Hermes:License";

    internal static string? Resolve(IConfiguration configuration)
        => Resolve(configuration, Environment.GetEnvironmentVariable);

    internal static string? Resolve(IConfiguration configuration, Func<string, string?> getEnvVar)
    {
        var envValue = getEnvVar(EnvVarName)?.Trim();
        if (!string.IsNullOrEmpty(envValue))
            return envValue;

        var configValue = configuration[ConfigKey]?.Trim();
        if (!string.IsNullOrEmpty(configValue))
            return configValue;

        return null;
    }
}
