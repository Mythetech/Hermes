// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Hermes.Tests.Web.WebView;

[Collection("HeadlessWebView")]
public class WebViewEventDispatchTests
{
    [Fact]
    public async Task Click_ReachesTheHandler_RerendersAndCompletesTheJsCall()
    {
        ReadyComponent.Reset();
        await using var host = HeadlessWebViewHost.Create();
        await host.AddRootComponentAsync(typeof(ReadyComponent), "#app", ParameterView.Empty);
        await host.AttachPageAsync();

        // What blazor.webview.js sends for a click on the button: a BeginInvokeDotNet call on
        // object 1 (the renderer interop methods) with the descriptor and the event args JSON.
        // The first event handler the renderer allocates has id 1.
        const string click = """__bwv:["BeginInvokeDotNet","1",null,"DispatchEventAsync",1,"[{\"eventHandlerId\":1,\"eventName\":\"click\"},{\"type\":\"click\",\"detail\":1,\"button\":0}]"]""";
        await host.ReceiveAsync(click);
        var secondBatch = await host.WaitForSentStartingWithAsync("""__bwv:["RenderBatch",2,""");
        await host.AcknowledgeRenderBatchAsync(secondBatch);
        // The completion is sent once the dispatch task settles, a few continuations after the
        // acknowledgment, so it has to be awaited rather than asserted on immediately.
        var completion = await host.WaitForSentAsync("EndInvokeDotNet");

        Assert.Equal(1, ReadyComponent.Clicks);
        // The dispatch task is a Task<VoidTaskResult>, and DotNetDispatcher serializes that empty
        // struct as {} exactly as upstream does; JavaScript ignores the value.
        Assert.Equal("""__bwv:["EndInvokeDotNet","1",true,"{}"]""", completion);
        Assert.Empty(host.UnhandledExceptions);
    }
}
