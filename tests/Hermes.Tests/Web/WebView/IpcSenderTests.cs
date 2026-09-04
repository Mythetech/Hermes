// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Xunit;

namespace Hermes.Tests.Web.WebView;

public class IpcSenderTests
{
    private readonly List<string> _sent = new();
    private readonly List<Exception> _unhandled = new();

    private IpcSender CreateSender(Action<string>? messageDispatcher = null)
        => new(new InlineDispatcher(), messageDispatcher ?? _sent.Add, _unhandled.Add);

    [Fact]
    public void EachMethod_DispatchesTheWriterOutput()
    {
        var sender = CreateSender();

        sender.AttachToDocument(0, "#app");
        sender.Navigate("/", new NavigationOptions());
        sender.Refresh(false);
        sender.EndInvokeDotNet("1", true, "null");
        sender.SendByteArray(0, new byte[] { 1 });
        sender.SetHasLocationChangingListeners(false);
        sender.EndLocationChanging(2, true);
        sender.BeginInvokeJS(new JSInvocationInfo
        {
            AsyncHandle = 0,
            TargetInstanceId = 0,
            Identifier = "f",
            CallType = JSCallType.FunctionCall,
            ResultType = JSCallResultType.JSVoidResult,
            ArgsJson = "[]",
        });

        Assert.Equal(
        [
            """__bwv:["AttachToDocument",0,"#app"]""",
            """__bwv:["Navigate","/",{"forceLoad":false,"replaceHistoryEntry":false,"historyEntryState":null}]""",
            """__bwv:["Refresh",false]""",
            """__bwv:["EndInvokeDotNet","1",true,"null"]""",
            """__bwv:["SendByteArrayToJS",0,"AQ=="]""",
            """__bwv:["SetHasLocationChangingListeners",false]""",
            """__bwv:["EndLocationChanging",2,true]""",
            """__bwv:["BeginInvokeJS",0,"f","[]",3,0,1]""",
        ], _sent);
        Assert.Empty(_unhandled);
    }

    [Fact]
    public void NotifyUnhandledException_TellsThePageThenTheHost()
    {
        var sender = CreateSender();
        var exception = new InvalidOperationException("boom");

        sender.NotifyUnhandledException(exception);

        var message = Assert.Single(_sent);
        Assert.StartsWith("""__bwv:["NotifyUnhandledException","boom",""", message);
        Assert.Same(exception, Assert.Single(_unhandled));
    }

    [Fact]
    public void FailedSend_ReachesTheHost_WithoutRecursing()
    {
        var failure = new IOException("channel closed");
        var sender = CreateSender(_ => throw failure);

        sender.AttachToDocument(0, "#app");

        // The failed send is reported once. Reporting it also tries to send a
        // NotifyUnhandledException message, which fails too; that second failure must be
        // logged and dropped, not reported again, or the sender would recurse forever.
        Assert.Same(failure, Assert.Single(_unhandled));
    }
}
