// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Text.Json;
using Hermes.Diagnostics;
using Hermes.Testing;
using Hermes.Tests.Smoke;
using Hermes.Web.Interop;
using Xunit;

namespace Hermes.Tests.Web;

/// <summary>Watches HermesLogger's process-wide error event, so it runs alone.</summary>
[Collection(nameof(ProcessWideEventsCollection))]
public sealed class InteropBridgePageSignalTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private readonly ConcurrentQueue<string> _errors = new();
    private readonly List<InteropBridge> _bridges = [];

    public InteropBridgePageSignalTests() => HermesLogger.ErrorObserved += OnError;

    public void Dispose()
    {
        HermesLogger.ErrorObserved -= OnError;
        foreach (var bridge in _bridges)
            bridge.Stop();
    }

    [Fact]
    public async Task Ready_RaisesPageReady()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, backend) = Start(b => b.PageReady += () => ready.TrySetResult());

        backend.SimulateWebMessage("""{"type":"ready"}""");

        await ready.Task.WaitAsync(Wait);
    }

    [Fact]
    public async Task PageError_RaisesPageErrorReported_WithItsFields()
    {
        var reported = new TaskCompletionSource<PageError>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, backend) = Start(b => b.PageErrorReported += error => reported.TrySetResult(error));

        backend.SimulateWebMessage("""{"type":"page-error","source":"unhandledrejection","name":"TypeError","message":"boom","stack":"at main.js:1"}""");

        Assert.Equal(new PageError("unhandledrejection", "TypeError", "boom", "at main.js:1"), await reported.Task.WaitAsync(Wait));
    }

    [Fact]
    public async Task PageError_WithoutFields_UsesDefaults()
    {
        var reported = new TaskCompletionSource<PageError>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, backend) = Start(b => b.PageErrorReported += error => reported.TrySetResult(error));

        backend.SimulateWebMessage("""{"type":"page-error"}""");

        Assert.Equal(new PageError("error", "Error", "", null), await reported.Task.WaitAsync(Wait));
    }

    [Fact]
    public async Task ThrowingPageReadySubscriber_DoesNotStopTheReader()
    {
        var (_, backend) = Start(b =>
        {
            b.PageReady += () => throw new InvalidOperationException("subscriber");
            b.Handle("ping", () => "pong");
        });

        backend.SimulateWebMessage("""{"type":"ready"}""");
        backend.SimulateWebMessage("""{"type":"invoke","id":"1","method":"ping","args":[]}""");

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("pong", reply.GetProperty("value").GetString());
    }

    [Fact]
    public async Task HandlerException_IsLoggedAsAWarning_NotAnError()
    {
        var (_, backend) = Start(b => b.Handle("save", () => ThrowInvalid()));

        backend.SimulateWebMessage("""{"type":"invoke","id":"1","method":"save","args":[]}""");

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("error", reply.GetProperty("type").GetString());
        Assert.Equal("invalid", reply.GetProperty("message").GetString());
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task EventHandlerException_IsStillLoggedAsAnError()
    {
        var (_, backend) = Start(b => b.On("tick", () => throw new InvalidOperationException("listener")));

        backend.SimulateWebMessage("""{"type":"event","name":"tick","data":null}""");

        var deadline = DateTime.UtcNow + Wait;
        while (!_errors.Any(e => e.Contains("tick", StringComparison.Ordinal)))
        {
            Assert.True(DateTime.UtcNow < deadline, "The event handler's failure was never logged as an error.");
            await Task.Delay(5);
        }
    }

    private static string ThrowInvalid() => throw new InvalidOperationException("invalid");

    private void OnError(string message, Exception? exception) => _errors.Enqueue(message);

    private (InteropBridge Bridge, RecordingWindowBackend Backend) Start(Action<InteropBridge> configure)
    {
        var backend = new RecordingWindowBackend();
        var bridge = new InteropBridge(backend);
        _bridges.Add(bridge);
        configure(bridge);
        bridge.Start();
        return (bridge, backend);
    }

    private static async Task<IReadOnlyList<JsonElement>> SentAsync(RecordingWindowBackend backend, int count)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (backend.Recording.WebMessagesSent.Count < count && DateTime.UtcNow < deadline)
        {
            backend.ProcessPending();
            await Task.Delay(5);
        }

        Assert.True(backend.Recording.WebMessagesSent.Count >= count, "The bridge never replied.");
        return backend.Recording.WebMessagesSent.Select(m => JsonDocument.Parse(m).RootElement.Clone()).ToList();
    }
}
