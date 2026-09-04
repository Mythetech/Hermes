// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Xunit;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// blazor.webview.js is consumed unchanged from the upstream package, so every outgoing
/// envelope must be byte-identical to what Microsoft.AspNetCore.Components.WebView 10.0.11
/// produced. The expected strings were recorded from upstream's IpcCommon.Serialize.
/// </summary>
public class IpcMessageWriterTests
{
    [Fact]
    public void RenderBatch_EscapesPlusLikeUpstream()
    {
        Assert.Equal("""__bwv:["RenderBatch",1,"AQID\u002Bg=="]""", IpcMessageWriter.RenderBatch(1, new byte[] { 1, 2, 3, 250 }));
        Assert.Equal("""__bwv:["RenderBatch",2,"\u002B/\u002B/\u002B/\u002B/"]""", IpcMessageWriter.RenderBatch(2, new byte[] { 0xfb, 0xff, 0xbf, 0xfb, 0xff, 0xbf }));
    }

    [Fact]
    public void Navigate_WritesCamelCaseOptions()
    {
        var options = new NavigationOptions { ForceLoad = false, ReplaceHistoryEntry = true, HistoryEntryState = "st\"ate" };
        Assert.Equal("""__bwv:["Navigate","/counter?x=1",{"forceLoad":false,"replaceHistoryEntry":true,"historyEntryState":"st\u0022ate"}]""", IpcMessageWriter.Navigate("/counter?x=1", options));
        Assert.Equal("""__bwv:["Navigate","/",{"forceLoad":false,"replaceHistoryEntry":false,"historyEntryState":null}]""", IpcMessageWriter.Navigate("/", new NavigationOptions()));
    }

    [Fact]
    public void AttachToDocument_EscapesHtmlSensitiveAndNonAsciiCharacters()
    {
        Assert.Equal("""__bwv:["AttachToDocument",0,"#app"]""", IpcMessageWriter.AttachToDocument(0, "#app"));
        Assert.Equal("""__bwv:["AttachToDocument",1,"#app-\u00E9-\u003Cscript\u003E\u0026\u0027"]""", IpcMessageWriter.AttachToDocument(1, "#app-é-<script>&'"));
    }

    [Fact]
    public void EndInvokeDotNet_WritesResultOrErrorAndNull()
    {
        Assert.Equal("""__bwv:["EndInvokeDotNet","1",true,"{\u0022a\u0022:1}"]""", IpcMessageWriter.EndInvokeDotNet("1", true, "{\"a\":1}"));
        Assert.Equal("__bwv:[\"EndInvokeDotNet\",\"2\",false,\"System.Exception: boom\\n   at x\"]", IpcMessageWriter.EndInvokeDotNet("2", false, "System.Exception: boom\n   at x"));
        Assert.Equal("""__bwv:["EndInvokeDotNet","3",true,null]""", IpcMessageWriter.EndInvokeDotNet("3", true, null));
    }

    [Fact]
    public void NotifyUnhandledException_WritesMessageAndOptionalStackTrace()
    {
        Assert.Equal("__bwv:[\"NotifyUnhandledException\",\"msg \\u003Cb\\u003E\\u0026\",\"   at Foo.Bar()\\n   at Baz()\"]", IpcMessageWriter.NotifyUnhandledException("msg <b>&", "   at Foo.Bar()\n   at Baz()"));
        Assert.Equal("""__bwv:["NotifyUnhandledException","msg",null]""", IpcMessageWriter.NotifyUnhandledException("msg", null));
    }

    [Fact]
    public void BeginInvokeJS_WritesEnumsAsNumbersInUpstreamFieldOrder()
    {
        var attach = new JSInvocationInfo
        {
            AsyncHandle = 2,
            TargetInstanceId = 0,
            Identifier = "Blazor._internal.attachWebRendererInterop",
            CallType = JSCallType.FunctionCall,
            ResultType = JSCallResultType.JSVoidResult,
            ArgsJson = "[3,{\"__dotNetObject\":1},{},{}]",
        };
        Assert.Equal("""__bwv:["BeginInvokeJS",2,"Blazor._internal.attachWebRendererInterop","[3,{\u0022__dotNetObject\u0022:1},{},{}]",3,0,1]""", IpcMessageWriter.BeginInvokeJS(attach));

        var getValue = new JSInvocationInfo
        {
            AsyncHandle = 7,
            TargetInstanceId = 5,
            Identifier = "obj.method",
            CallType = JSCallType.GetValue,
            ResultType = JSCallResultType.Default,
            ArgsJson = null!,
        };
        // JSInvocationInfo.ArgsJson reports "[]" for a null value, so that is what goes on the wire.
        Assert.Equal("""__bwv:["BeginInvokeJS",7,"obj.method","[]",0,5,3]""", IpcMessageWriter.BeginInvokeJS(getValue));
    }

    [Fact]
    public void SendByteArrayToJS_DoesNotEscapeBase64LikeUpstream()
    {
        Assert.Equal("""__bwv:["SendByteArrayToJS",0,"AP8Q"]""", IpcMessageWriter.SendByteArrayToJS(0, new byte[] { 0, 255, 16 }));
        Assert.Equal("""__bwv:["SendByteArrayToJS",1,"+/+/+/+/"]""", IpcMessageWriter.SendByteArrayToJS(1, new byte[] { 0xfb, 0xff, 0xbf, 0xfb, 0xff, 0xbf }));
    }

    [Fact]
    public void BooleanAndIntMessages_MatchUpstream()
    {
        Assert.Equal("""__bwv:["SetHasLocationChangingListeners",true]""", IpcMessageWriter.SetHasLocationChangingListeners(true));
        Assert.Equal("""__bwv:["EndLocationChanging",3,false]""", IpcMessageWriter.EndLocationChanging(3, false));
        Assert.Equal("""__bwv:["Refresh",true]""", IpcMessageWriter.Refresh(true));
    }
}
