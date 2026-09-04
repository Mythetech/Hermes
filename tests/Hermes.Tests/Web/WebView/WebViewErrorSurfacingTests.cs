// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// Every failure in the host layer must reach the host and the page. Upstream let each of
/// these disappear into a discarded task, which is how a Native AOT build showed a blank
/// window with no diagnostic.
/// </summary>
[Collection("HeadlessWebView")]
public class WebViewErrorSurfacingTests
{
    [Fact]
    public async Task MalformedMessage_IsReportedToHostAndPage()
    {
        await using var host = HeadlessWebViewHost.Create();

        await host.ReceiveAsync("__bwv:[");

        Assert.IsAssignableFrom<JsonException>(Assert.Single(host.UnhandledExceptions));
        Assert.StartsWith("""__bwv:["NotifyUnhandledException",""", Assert.Single(host.SentSnapshot()));
    }

    [Fact]
    public async Task UnknownMessageType_IsReported()
    {
        await using var host = HeadlessWebViewHost.Create();

        await host.ReceiveAsync("""__bwv:["Bogus"]""");

        var ex = Assert.IsType<InvalidOperationException>(Assert.Single(host.UnhandledExceptions));
        Assert.Contains("Bogus", ex.Message);
    }

    [Fact]
    public async Task MessageBeforeAttach_IsReported()
    {
        await using var host = HeadlessWebViewHost.Create();

        await host.ReceiveAsync("""__bwv:["OnRenderCompleted",1,null]""");

        Assert.Contains("no page is attached", Assert.Single(host.UnhandledExceptions).Message);
    }

    [Fact]
    public async Task UnexpectedRenderAcknowledgement_IsReported()
    {
        ReadyComponent.Reset();
        await using var host = HeadlessWebViewHost.Create();
        await host.AddRootComponentAsync(typeof(ReadyComponent), "#app", ParameterView.Empty);
        await host.AttachPageAsync();

        await host.ReceiveAsync("""__bwv:["OnRenderCompleted",99,null]""");

        Assert.Contains("99", Assert.Single(host.UnhandledExceptions).Message);
    }

    [Fact]
    public async Task RenderErrorReportedByThePage_IsSurfaced()
    {
        ReadyComponent.Reset();
        await using var host = HeadlessWebViewHost.Create();
        await host.AddRootComponentAsync(typeof(ReadyComponent), "#app", ParameterView.Empty);

        var attach = host.ReceiveAsync("""__bwv:["AttachPage","app://localhost/","app://localhost/"]""");
        await host.WaitForSentAsync("RenderBatch");
        await host.ReceiveAsync("""__bwv:["OnRenderCompleted",1,"TypeError: cannot read renderBatch"]""");
        await attach;

        Assert.Contains("cannot read renderBatch", Assert.Single(host.UnhandledExceptions).Message);
        Assert.Contains(host.SentSnapshot(), m => m.StartsWith("""__bwv:["NotifyUnhandledException",""", StringComparison.Ordinal));
        Assert.Equal(0, ReadyComponent.AfterRenderCount);
    }

    [Fact]
    public async Task NonBlazorMessage_IsIgnored()
    {
        await using var host = HeadlessWebViewHost.Create();

        await host.ReceiveAsync("app-specific message on the same channel");

        Assert.Empty(host.UnhandledExceptions);
        Assert.Empty(host.SentSnapshot());
    }
}
