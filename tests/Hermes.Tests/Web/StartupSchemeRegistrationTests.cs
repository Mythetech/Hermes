// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Blazor;
using Hermes.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hermes.Tests.Web;

/// <summary>
/// The startup scheme handler must be registered before the native window exists
/// and must serve the host page while composition is still running, on every
/// platform, so the WebView can boot in parallel with managed startup.
/// </summary>
public class StartupSchemeRegistrationTests
{
    private const string HostHtml =
        "<html><body><div id=\"app\"></div><script src=\"_framework/blazor.webview.js\"></script></body></html>";

    private static TestFileProvider Assets() => new TestFileProvider()
        .Add("index.html", HostHtml)
        .Add("_framework/blazor.webview.js", "console.log('boot')");

    [Theory]
    [InlineData(HermesPlatform.Windows)]
    [InlineData(HermesPlatform.macOS)]
    [InlineData(HermesPlatform.Linux)]
    public void Build_RegistersTheAppScheme_BeforeInitialize(HermesPlatform platform)
    {
        var backend = new RecordingWindowBackend { Platform = platform };

        using var app = BuildApp(backend);

        var calls = backend.Recording.MethodCalls.Select(c => c.MethodName).ToList();
        var registerIndex = calls.IndexOf("RegisterCustomScheme");
        var initializeIndex = calls.IndexOf("Initialize");
        Assert.True(registerIndex >= 0, "Build() never registered the startup scheme.");
        Assert.True(registerIndex < initializeIndex, "The scheme must be registered before the native window is created.");

        var registered = backend.Recording.MethodCalls.First(c => c.MethodName == "RegisterCustomScheme");
        Assert.Equal(new Uri(HermesWebViewManager.AppBaseUri).Scheme, registered.Arguments[0]);
    }

    [Fact]
    public void Build_ServesInlinedHostPage_WhileCompositionIsStillRunning_AndIdenticalBytesAfterwards()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };
        (Stream? Content, string? ContentType) early = default;

        using var app = BuildApp(backend, builder =>
        {
            // The factory runs on the composition worker, so a request issued from
            // inside it is by definition a request that arrived before the manager.
            ((IHostApplicationBuilder)builder).ConfigureContainer(new CallbackServiceProviderFactory(() =>
                early = backend.SimulateSchemeRequest(HermesWebViewManager.AppBaseUri)));
        });

        Assert.Equal("text/html", early.ContentType);
        var earlyHtml = ReadAll(early.Content);
        Assert.Contains("<script>console.log('boot')</script>", earlyHtml);

        // After Build() the manager answers; the WebView must see the same page
        // whichever handler served it.
        var (lateContent, lateContentType) = backend.SimulateSchemeRequest(HermesWebViewManager.AppBaseUri);
        Assert.StartsWith("text/html", lateContentType);
        Assert.Equal(earlyHtml, ReadAll(lateContent));
    }

    private static string ReadAll(Stream? stream)
    {
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Build_ServesRequests_AfterCompositionCompletes()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend);
        var (content, contentType) = backend.SimulateSchemeRequest(HermesWebViewManager.AppBaseUri + "counter");

        Assert.NotNull(content);
        Assert.StartsWith("text/html", contentType);
    }

    private static DisposableApp BuildApp(RecordingWindowBackend backend, Action<HermesBlazorAppBuilder>? configure = null)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        builder.ForceDevServer(false);
        builder.UseFileProvider(Assets());
        configure?.Invoke(builder);
        return new DisposableApp(HermesBlazorAppBuilder.BuildForTest(builder, backend));
    }

    /// <summary>
    /// Runs a callback on the composition worker right before the service provider
    /// is built, then builds the provider normally.
    /// </summary>
    internal sealed class CallbackServiceProviderFactory(Action onCreate) : IServiceProviderFactory<IServiceCollection>
    {
        public IServiceCollection CreateBuilder(IServiceCollection services) => services;

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            onCreate();
            return containerBuilder.BuildServiceProvider();
        }
    }

    internal sealed class DisposableApp(HermesBlazorApp app) : IDisposable
    {
        public HermesBlazorApp App => app;
        public void Dispose() => app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
