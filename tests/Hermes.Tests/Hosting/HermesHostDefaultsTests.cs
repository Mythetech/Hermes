// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Hosting;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hermes.Tests.Hosting;

public sealed class HermesHostDefaultsTests
{
    [Fact]
    public void ResolveEnvironmentName_ReadsDotnetEnvironment()
    {
        var name = HermesHostDefaults.ResolveEnvironmentName([], Variables(("DOTNET_ENVIRONMENT", "Development")));

        Assert.Equal("Development", name);
    }

    [Fact]
    public void ResolveEnvironmentName_CommandLineWins()
    {
        var name = HermesHostDefaults.ResolveEnvironmentName(
            ["--environment", "Staging"],
            Variables(("DOTNET_ENVIRONMENT", "Development")));

        Assert.Equal("Staging", name);
    }

    [Fact]
    public void ResolveEnvironmentName_NullWhenUnset()
    {
        var name = HermesHostDefaults.ResolveEnvironmentName(null, Variables());

        Assert.Null(name);
    }

    [Fact]
    public void BlazorBuilder_EnvironmentFromCommandLine_IsDevelopment()
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder(["--environment", "Development"]);

        Assert.True(builder.Environment.IsDevelopment());
    }

    private static Func<string, string?> Variables(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(v => v.Name == name).Value;
}
