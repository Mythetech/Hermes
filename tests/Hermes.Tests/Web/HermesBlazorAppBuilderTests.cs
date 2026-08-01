// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Blazor;
using Hermes.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Web;

public class HermesBlazorAppBuilderTests
{
    [Fact]
    public void ComposeServices_ProducesServiceProviderAndFileProvider()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        Assert.NotNull(composition.ServiceProvider);
        Assert.NotNull(composition.FileProvider);
        Assert.NotNull(composition.ServiceProvider.GetService<IServiceProvider>());
        Assert.NotNull(composition.LicenseResult);
    }

    [Fact]
    public void ComposeServices_TouchesNoNativeWindowMembers()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        _ = HermesBlazorAppBuilder.ComposeForTest(builder, backend);

        Assert.DoesNotContain(backend.Recording.MethodCalls, c => c.MethodName == "Initialize");
        Assert.DoesNotContain(backend.Recording.MethodCalls, c => c.MethodName == "Show");
        Assert.DoesNotContain(backend.Recording.MethodCalls, c => c.MethodName == "InitializeApplication");
    }

    [Fact]
    public void ComposeServices_RunsOnWorkerThread()
    {
        var backend = new RecordingWindowBackend();
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        var composition = Task.Run(() => HermesBlazorAppBuilder.ComposeForTest(builder, backend))
            .GetAwaiter().GetResult();

        Assert.NotNull(composition.ServiceProvider);
    }
}
