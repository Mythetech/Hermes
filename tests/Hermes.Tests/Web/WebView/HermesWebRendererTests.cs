// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// The wire sequence blazor.webview.js expects at startup, pinned against what upstream
/// 10.0.11 sent for the same component (recorded 2026-09-03). The one deliberate difference is
/// the attach call's async handle: upstream allocated 2 and discarded the completion, Hermes
/// sends 0 so no completion is requested.
/// </summary>
[Collection("HeadlessWebView")]
public class HermesWebRendererTests
{
    private const string ExpectedAttach = """__bwv:["BeginInvokeJS",0,"Blazor._internal.attachWebRendererInterop","[3,{\u0022__dotNetObject\u0022:1},{},{}]",3,0,1]""";
    private const string ExpectedAttachToDocument = """__bwv:["AttachToDocument",0,"#app"]""";
    private const string ExpectedFirstBatch = """__bwv:["RenderBatch",1,"AAAAAAEAAAABAAAAAAAAAAAAAAD/////AQAAAAAAAAADAAAAAQAAAAMAAAAAAAAAAAAAAAAAAAADAAAAAQAAAP////8BAAAAAAAAAAIAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGYnV0dG9uB29uY2xpY2sFUkVBRFloAAAAbwAAAHcAAAAYAAAAIAAAAGAAAABkAAAAfQAAAA=="]""";

    [Fact]
    public async Task AttachPage_SendsInteropAttach_ThenDocumentAttach_ThenTheRenderBatch()
    {
        ReadyComponent.Reset();
        await using var host = HeadlessWebViewHost.Create();
        await host.AddRootComponentAsync(typeof(ReadyComponent), "#app", ParameterView.Empty);

        await host.AttachPageAsync();

        var sent = host.SentSnapshot();
        Assert.Equal(ExpectedAttach, sent[0]);
        Assert.Equal(ExpectedAttachToDocument, sent[1]);
        Assert.Equal(ExpectedFirstBatch, sent[2]);
        Assert.Equal(3, sent.Length);
        Assert.Equal(1, ReadyComponent.AfterRenderCount);
        Assert.Empty(host.UnhandledExceptions);
    }

    [Fact]
    public async Task Navigate_ResolvesAgainstTheAppBase()
    {
        await using var host = HeadlessWebViewHost.Create();

        host.Navigate("/");

        Assert.Equal(new Uri("app://localhost/"), Assert.Single(host.Navigations));
    }

    [Fact]
    public async Task RootComponentAddedAfterAttach_RendersImmediately()
    {
        ReadyComponent.Reset();
        await using var host = HeadlessWebViewHost.Create();
        await host.AddRootComponentAsync(typeof(ReadyComponent), "#app", ParameterView.Empty);
        await host.AttachPageAsync();

        var add = host.AddRootComponentAsync(typeof(ReadyComponent), "#second", ParameterView.Empty);
        var batch = await host.WaitForSentStartingWithAsync("""__bwv:["RenderBatch",2,""");
        await host.AcknowledgeRenderBatchAsync(batch);
        await add;

        Assert.Contains("""__bwv:["AttachToDocument",1,"#second"]""", host.SentSnapshot());
        Assert.Equal(2, ReadyComponent.AfterRenderCount);
    }
}
