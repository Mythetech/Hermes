// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Foundation;
using ObjCRuntime;
using WebKit;

namespace Hermes.Mobile.iOS.WebView;

/// <summary>
/// Forwards WKWebView → native messages to the WebViewManager's MessageReceived pump.
/// Name "webwindowinterop" must match the JS side in BlazorInitScript.
/// </summary>
[Adopts("WKScriptMessageHandler")]
internal sealed class ScriptMessageHandler : NSObject, IWKScriptMessageHandler
{
    public const string Name = "webwindowinterop";

    static ScriptMessageHandler()
        => ProtocolAdoption.Ensure<ScriptMessageHandler>("WKScriptMessageHandler");

    private readonly Action<Uri, string> _onMessage;
    private readonly Uri _appOrigin;

    public ScriptMessageHandler(Uri appOrigin, Action<Uri, string> onMessage)
    {
        _appOrigin = appOrigin;
        _onMessage = onMessage;
    }

    public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
    {
        var body = ((NSString)message.Body).ToString();
        _onMessage(_appOrigin, body);
    }
}
