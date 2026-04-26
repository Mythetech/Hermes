// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Blazor.Licensing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Hermes.Tests.Licensing;

public sealed class LicenseKeyResolverTests
{
    private static IConfiguration BuildConfig(string? value = null)
    {
        var builder = new ConfigurationBuilder();
        if (value is not null)
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hermes:License"] = value
            });
        }
        return builder.Build();
    }

    [Fact]
    public void Returns_env_var_when_set()
    {
        var result = LicenseKeyResolver.Resolve(
            BuildConfig(),
            getEnvVar: name => name == "HERMES_LICENSE" ? "HERMES-1-envtoken" : null);

        Assert.Equal("HERMES-1-envtoken", result);
    }

    [Fact]
    public void Returns_config_when_env_var_missing()
    {
        var result = LicenseKeyResolver.Resolve(
            BuildConfig("HERMES-1-configtoken"),
            getEnvVar: _ => null);

        Assert.Equal("HERMES-1-configtoken", result);
    }

    [Fact]
    public void Env_var_takes_priority_over_config()
    {
        var result = LicenseKeyResolver.Resolve(
            BuildConfig("HERMES-1-configtoken"),
            getEnvVar: name => name == "HERMES_LICENSE" ? "HERMES-1-envtoken" : null);

        Assert.Equal("HERMES-1-envtoken", result);
    }

    [Fact]
    public void Returns_null_when_nothing_configured()
    {
        var result = LicenseKeyResolver.Resolve(
            BuildConfig(),
            getEnvVar: _ => null);

        Assert.Null(result);
    }

    [Fact]
    public void Trims_whitespace_from_env_var()
    {
        var result = LicenseKeyResolver.Resolve(
            BuildConfig(),
            getEnvVar: name => name == "HERMES_LICENSE" ? "  HERMES-1-token  " : null);

        Assert.Equal("HERMES-1-token", result);
    }

    [Fact]
    public void Empty_env_var_falls_through_to_config()
    {
        var result = LicenseKeyResolver.Resolve(
            BuildConfig("HERMES-1-configtoken"),
            getEnvVar: name => name == "HERMES_LICENSE" ? "  " : null);

        Assert.Equal("HERMES-1-configtoken", result);
    }
}
