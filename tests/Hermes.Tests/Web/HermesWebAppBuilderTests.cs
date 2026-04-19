// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Web;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class HermesWebAppBuilderTests
{
    [Fact]
    public void Create_ReturnsNonNullBuilder()
    {
        var builder = HermesWebAppBuilder.Create();

        Assert.NotNull(builder);
    }

    [Fact]
    public void Create_WithArgs_ReturnsNonNullBuilder()
    {
        var builder = HermesWebAppBuilder.Create(["--dev"]);

        Assert.NotNull(builder);
    }

    [Fact]
    public void FluentMethods_ReturnSameBuilder()
    {
        var builder = HermesWebAppBuilder.Create();

        var result = builder
            .ConfigureWindow(opts => opts.Title = "Test")
            .UseStaticFiles()
            .UseSpaFallback()
            .UseDevServer("http://localhost:5173")
            .UseInteropBridge(opts => { });

        Assert.Same(builder, result);
    }
}
