// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Web;

public class RendererWarmupTests
{
    [Fact]
    public void Run_CompletesAgainstComposedProvider()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        RendererWarmup.Run(composition.ServiceProvider);
    }

    [Fact]
    public void Run_SwallowsFailures_WhenProviderIsEmpty()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        RendererWarmup.Run(provider);
    }
}
