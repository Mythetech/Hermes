// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Hermes.Web.Interop;
using Xunit;

namespace Hermes.Tests.Web;

[SuppressMessage("Trimming", "IL2026")]
[SuppressMessage("AOT", "IL3050")]
public sealed class InteropBridgeOptionsTests
{
    [Fact]
    public void Register_AddsHandlerToInvokeHandlers()
    {
        var options = new InteropBridgeOptions();

        options.Register("ping", () => (object?)"pong");

        Assert.True(options.InvokeHandlers.ContainsKey("ping"));
    }

    [Fact]
    public async Task Register_TypedResult_HandlerReturnsCorrectValue()
    {
        var options = new InteropBridgeOptions();

        options.Register<int>("getCount", () => 42);

        var result = await options.InvokeHandlers["getCount"]([]);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RegisterAsync_HandlerReturnsCorrectValue()
    {
        var options = new InteropBridgeOptions();

        options.RegisterAsync<string>("fetchName", () => Task.FromResult("Hermes"));

        var result = await options.InvokeHandlers["fetchName"]([]);
        Assert.Equal("Hermes", result);
    }

    [Fact]
    public void On_AddsHandlerToEventHandlers()
    {
        var options = new InteropBridgeOptions();

        options.On("click", () => { });

        Assert.True(options.EventHandlers.ContainsKey("click"));
        Assert.Single(options.EventHandlers["click"]);
    }

    [Fact]
    public void On_MultipleHandlersForSameEvent_Accumulates()
    {
        var options = new InteropBridgeOptions();

        options.On("click", () => { });
        options.On("click", () => { });

        Assert.Equal(2, options.EventHandlers["click"].Count);
    }

    [Fact]
    public async Task Register_SameMethodName_OverwritesPrevious()
    {
        var options = new InteropBridgeOptions();

        options.Register<string>("greet", () => "hello");
        options.Register<string>("greet", () => "goodbye");

        var result = await options.InvokeHandlers["greet"]([]);
        Assert.Equal("goodbye", result);
    }
}
