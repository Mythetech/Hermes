// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Hermes.Web.Interop;

namespace WebHelloWorld;

internal sealed record EchoPayload(string Text, int Number);

/// <summary>
/// Sends the page an object and waits for it to come back, so smoke runs cover both bridge directions.
/// </summary>
internal sealed class BridgeEchoCheck(InteropBridge bridge) : IHermesSmokeCheck
{
    public string Name => "web-hello/bridge-echo";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var expected = new EchoPayload("hermes", 42);
        var reply = new TaskCompletionSource<EchoPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = bridge.On<EchoPayload>("smoke.echo.reply", payload => reply.TrySetResult(payload));

        bridge.Send("smoke.echo", expected);
        var echoed = await reply.Task.WaitAsync(cancellationToken);

        if (echoed != expected)
            throw new InvalidOperationException($"The page echoed {echoed}, not {expected}.");
    }
}

/// <summary>
/// Makes the page throw, so CI can prove that page errors fail a smoke run. Registered only with WEB_SAMPLE_FAULT=page.
/// </summary>
internal sealed class PageFaultCheck(InteropBridge bridge) : IHermesSmokeCheck
{
    public string Name => "web-hello/page-fault";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var thrown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = bridge.On("smoke.fault.thrown", () => thrown.TrySetResult());

        bridge.Send("smoke.fault");
        await thrown.Task.WaitAsync(cancellationToken);
    }
}
