// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Hermes.Abstractions;
using Hermes.Blazor;
using Hermes.Blazor.DevServer;
using Hermes.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Web;

public class RequestHandlerTests
{
    private static readonly string AppScheme = new Uri(HermesWebViewManager.AppBaseUri).Scheme;

    [Fact]
    public void MappedPath_IsServedByTheHandler_WithTheFullUrl()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };
        string? received = null;

        using var app = BuildApp(backend, builder => builder.MapRequestHandler("/media", url =>
        {
            received = url;
            return (Text("pixel"), "image/png");
        }));

        var url = HermesWebViewManager.AppBaseUri + "media/cards/12345.png?size=small";
        var (content, contentType) = Request(backend, url);

        Assert.Equal("pixel", ReadText(content));
        Assert.Equal("image/png", contentType);
        Assert.Equal(url, received);
    }

    [Fact]
    public void MappedPath_ItselfIsServedByTheHandler()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend, builder => builder.MapRequestHandler("/status", _ => (Text("ok"), "text/plain")));

        Assert.Equal("ok", ReadText(Request(backend, HermesWebViewManager.AppBaseUri + "status").Content));
    }

    [Fact]
    public void TypedHandler_IsResolvedFromTheAppServices()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend, builder =>
        {
            builder.Services.AddSingleton(new Greeting("hello from DI"));
            builder.MapRequestHandler<GreetingHandler>("/greet");
        });

        var (content, contentType) = Request(backend, HermesWebViewManager.AppBaseUri + "greet/anyone");

        Assert.Equal("hello from DI", ReadText(content));
        Assert.Equal("text/plain", contentType);
    }

    [Fact]
    public void TypedHandler_KeepsTheAppsOwnRegistration()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };
        var registered = new GreetingHandler(new Greeting("registered by the app"));

        using var app = BuildApp(backend, builder =>
        {
            builder.Services.AddSingleton(registered);
            builder.MapRequestHandler<GreetingHandler>("/greet");
        });

        Assert.Equal("registered by the app", ReadText(Request(backend, HermesWebViewManager.AppBaseUri + "greet").Content));
    }

    [Fact]
    public void LongestMappedPath_Wins()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend, builder => builder
            .MapRequestHandler("/media", _ => (Text("media"), "text/plain"))
            .MapRequestHandler("/media/thumbs", _ => (Text("thumbs"), "text/plain")));

        Assert.Equal("thumbs", ReadText(Request(backend, HermesWebViewManager.AppBaseUri + "media/thumbs/a.png").Content));
        Assert.Equal("media", ReadText(Request(backend, HermesWebViewManager.AppBaseUri + "media/a.png").Content));
    }

    [Theory]
    [InlineData("mediax/a.png")]
    [InlineData("other/media/a.png")]
    [InlineData("")]
    public void PathsOutsideTheMapping_DoNotReachTheHandler(string path)
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };
        var calls = 0;

        using var app = BuildApp(backend, builder => builder.MapRequestHandler("/media", _ =>
        {
            calls++;
            return (Text("media"), "text/plain");
        }));

        Request(backend, HermesWebViewManager.AppBaseUri + path);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void MatchingIgnoresCase_AndReadsEscapedPaths()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend, builder => builder.MapRequestHandler("/My Media", _ => (Text("found"), "text/plain")));

        Assert.Equal("found", ReadText(Request(backend, HermesWebViewManager.AppBaseUri + "my%20media/a.png").Content));
    }

    [Fact]
    public void HandlerThatThrows_AnswersNotFound()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend, builder => builder.MapRequestHandler("/media", _ => throw new IOException("disk gone")));

        var (content, contentType) = Request(backend, HermesWebViewManager.AppBaseUri + "media/a.png");

        Assert.Null(content);
        Assert.Null(contentType);
    }

    [Fact]
    public void HandlerReturningNoContent_DoesNotFallBackToTheHostPage()
    {
        var backend = new RecordingWindowBackend { Platform = HermesPlatform.macOS };

        using var app = BuildApp(backend, builder => builder.MapRequestHandler("/media", _ => (null, null)));

        var (content, _) = Request(backend, HermesWebViewManager.AppBaseUri + "media/missing");

        Assert.Null(content);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("media")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("/media?size=small")]
    [InlineData("/media#top")]
    [InlineData("/_framework")]
    [InlineData("/_content/Some.Library")]
    [InlineData("/_hermes")]
    public void Map_RejectsPathsThatCannotBeMapped(string path)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        Assert.ThrowsAny<ArgumentException>(() => builder.MapRequestHandler(path, _ => (null, null)));
    }

    [Fact]
    public void Map_RejectsAPathThatIsAlreadyMapped()
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        builder.MapRequestHandler("/media", _ => (null, null));

        Assert.Throws<InvalidOperationException>(() => builder.MapRequestHandler("/Media/", _ => (null, null)));
    }

    [Fact]
    public void Map_AcceptsPathsThatOnlyShareAPrefixWithReservedOnes()
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        builder.MapRequestHandler("/_frameworks", _ => (null, null));
        builder.MapRequestHandler("/content", _ => (null, null));
    }

    [Fact]
    public async Task DevServer_ServesMappedPaths()
    {
        var handlers = new HermesRequestHandlers();
        handlers.Map("/media", services => url => url.Contains("missing") ? (null, null) : (Text("pixel"), "image/png"));
        handlers.Map("/files", services => url => (Text("{}"), null));
        handlers.Attach(new ServiceCollection().BuildServiceProvider());

        var wwwroot = Directory.CreateTempSubdirectory("hermes-devserver-");
        try
        {
            await using var server = await HermesDevServer.StartAsync("index.html", wwwroot.FullName, handlers);
            using var http = new HttpClient { BaseAddress = new Uri(server.BaseUrl) };

            using var image = await http.GetAsync("/media/cards/1.png?size=small");
            using var inferred = await http.GetAsync("/files/data.json");
            using var missing = await http.GetAsync("/media/missing.png");

            Assert.Equal("pixel", await image.Content.ReadAsStringAsync());
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
            Assert.Equal("application/json", inferred.Content.Headers.ContentType?.MediaType);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            wwwroot.Delete(recursive: true);
        }
    }

    private static (Stream? Content, string? ContentType) Request(RecordingWindowBackend backend, string url) =>
        backend.TestCustomScheme(AppScheme, url) ?? throw new InvalidOperationException($"No handler is registered for the '{AppScheme}' scheme.");

    private static MemoryStream Text(string text) => new(Encoding.UTF8.GetBytes(text));

    private static string? ReadText(Stream? content)
    {
        if (content is null)
            return null;

        using var reader = new StreamReader(content);
        return reader.ReadToEnd();
    }

    private static DisposableApp BuildApp(RecordingWindowBackend backend, Action<HermesBlazorAppBuilder> configure)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        builder.ForceDevServer(false);
        configure(builder);
        return new DisposableApp(HermesBlazorAppBuilder.BuildForTest(builder, backend));
    }

    private sealed class DisposableApp : IDisposable
    {
        public DisposableApp(HermesBlazorApp app) => App = app;

        public HermesBlazorApp App { get; }

        public void Dispose() => App.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed record Greeting(string Text);

    private sealed class GreetingHandler(Greeting greeting) : IHermesRequestHandler
    {
        public (Stream? Content, string? ContentType) HandleRequest(string url) =>
            (new MemoryStream(Encoding.UTF8.GetBytes(greeting.Text)), "text/plain");
    }
}
