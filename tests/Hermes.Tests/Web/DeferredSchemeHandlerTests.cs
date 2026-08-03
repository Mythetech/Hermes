// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Xunit;

namespace Hermes.Tests.Web;

public class DeferredSchemeHandlerTests
{
    [Fact]
    public void Handle_DelegatesToInner_WhenInnerAlreadySet()
    {
        var handler = new DeferredSchemeHandler(TimeSpan.FromSeconds(1));
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        handler.SetInner(_ => (stream, "text/html"));

        var result = handler.Handle("app://localhost/");

        Assert.Same(stream, result.Content);
        Assert.Equal("text/html", result.ContentType);
    }

    [Fact]
    public void Handle_ReturnsNulls_WhenInnerNeverSetWithinTimeout()
    {
        var handler = new DeferredSchemeHandler(TimeSpan.FromMilliseconds(50));

        var result = handler.Handle("app://localhost/");

        Assert.Null(result.Content);
        Assert.Null(result.ContentType);
    }

    [Fact]
    public void Handle_WaitsForInner_WhenSetConcurrently()
    {
        // A dedicated thread keeps this test independent of thread pool
        // saturation on loaded CI runners; Task.Run here starved past the
        // handler timeout and flaked.
        var handler = new DeferredSchemeHandler(TimeSpan.FromSeconds(30));
        var setThread = new Thread(() =>
        {
            Thread.Sleep(50);
            handler.SetInner(_ => (null, "application/json"));
        });
        setThread.Start();

        var result = handler.Handle("app://localhost/data.json");

        Assert.Equal("application/json", result.ContentType);
        setThread.Join();
    }

    [Fact]
    public void Handle_PassesUrlThroughToInner()
    {
        var handler = new DeferredSchemeHandler(TimeSpan.FromSeconds(1));
        string? seenUrl = null;
        handler.SetInner(url => { seenUrl = url; return (null, null); });

        handler.Handle("app://localhost/index.html");

        Assert.Equal("app://localhost/index.html", seenUrl);
    }
}
