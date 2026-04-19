// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Hermes.Testing;
using Hermes.Web.Interop;
using Xunit;

namespace Hermes.Tests.Web;

[SuppressMessage("Trimming", "IL2026")]
[SuppressMessage("AOT", "IL3050")]
public sealed class InteropBridgeTests
{
    private static (InteropBridge Bridge, RecordingWindowBackend Backend) CreateBridge(
        Action<InteropBridgeOptions>? configure = null)
    {
        var backend = new RecordingWindowBackend();
        var options = new InteropBridgeOptions();
        configure?.Invoke(options);
        var bridge = new InteropBridge(backend, options);
        return (bridge, backend);
    }

    [Fact]
    public void Send_SendsEventEnvelopeToBackend()
    {
        var (bridge, backend) = CreateBridge();

        bridge.Send("greeting", "hello");

        Assert.Single(backend.Recording.WebMessagesSent);
        var json = backend.Recording.WebMessagesSent[0];
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("event", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("greeting", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void Send_WithNullData_SendsEnvelopeWithNullData()
    {
        var (bridge, backend) = CreateBridge();

        bridge.Send("ping");

        var json = backend.Recording.WebMessagesSent[0];
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ping", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Invoke_RegisteredMethod_SendsResultEnvelope()
    {
        var (bridge, backend) = CreateBridge(opts =>
        {
            opts.Register<string>("greet", () => "hello");
        });

        backend.SimulateWebMessage("""{"type":"invoke","id":"1","method":"greet","args":[]}""");

        await Task.Delay(50);

        var resultMessages = backend.Recording.WebMessagesSent;
        Assert.Single(resultMessages);

        using var doc = JsonDocument.Parse(resultMessages[0]);
        Assert.Equal("result", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("1", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("hello", doc.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Invoke_UnknownMethod_SendsErrorEnvelope()
    {
        var (bridge, backend) = CreateBridge();

        backend.SimulateWebMessage("""{"type":"invoke","id":"2","method":"unknown","args":[]}""");

        await Task.Delay(50);

        var resultMessages = backend.Recording.WebMessagesSent;
        Assert.Single(resultMessages);

        using var doc = JsonDocument.Parse(resultMessages[0]);
        Assert.Equal("error", doc.RootElement.GetProperty("type").GetString());
        Assert.Contains("Method not found", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Invoke_HandlerThrows_SendsErrorEnvelope()
    {
        var (bridge, backend) = CreateBridge(opts =>
        {
            opts.Register<string>("fail", () => throw new InvalidOperationException("boom"));
        });

        backend.SimulateWebMessage("""{"type":"invoke","id":"3","method":"fail","args":[]}""");

        await Task.Delay(50);

        var resultMessages = backend.Recording.WebMessagesSent;
        Assert.Single(resultMessages);

        using var doc = JsonDocument.Parse(resultMessages[0]);
        Assert.Equal("error", doc.RootElement.GetProperty("type").GetString());
        Assert.Contains("boom", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Invoke_AsyncHandler_SendsResultEnvelope()
    {
        var (bridge, backend) = CreateBridge(opts =>
        {
            opts.RegisterAsync<string>("asyncGreet", () => Task.FromResult("async hello"));
        });

        backend.SimulateWebMessage("""{"type":"invoke","id":"4","method":"asyncGreet","args":[]}""");

        await Task.Delay(50);

        var resultMessages = backend.Recording.WebMessagesSent;
        Assert.Single(resultMessages);

        using var doc = JsonDocument.Parse(resultMessages[0]);
        Assert.Equal("result", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("async hello", doc.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public void Event_RegisteredHandler_InvokesHandler()
    {
        var received = false;
        var (bridge, backend) = CreateBridge(opts =>
        {
            opts.On("click", () => received = true);
        });

        backend.SimulateWebMessage("""{"type":"event","name":"click"}""");

        Assert.True(received);
    }

    [Fact]
    public void Event_UnregisteredName_DoesNotThrow()
    {
        var (bridge, backend) = CreateBridge();

        var ex = Record.Exception(() =>
            backend.SimulateWebMessage("""{"type":"event","name":"unknown"}"""));

        Assert.Null(ex);
    }

    [Fact]
    public void NonJsonMessage_DoesNotThrow()
    {
        var (bridge, backend) = CreateBridge();

        var ex = Record.Exception(() =>
            backend.SimulateWebMessage("this is not json"));

        Assert.Null(ex);
    }

    [Fact]
    public void Detach_StopsProcessingMessages()
    {
        var callCount = 0;
        var (bridge, backend) = CreateBridge(opts =>
        {
            opts.On("ping", () => callCount++);
        });

        backend.SimulateWebMessage("""{"type":"event","name":"ping"}""");
        Assert.Equal(1, callCount);

        bridge.Detach();

        backend.SimulateWebMessage("""{"type":"event","name":"ping"}""");
        Assert.Equal(1, callCount);
    }
}
