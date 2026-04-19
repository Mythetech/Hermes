// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Android.OS;
using Android.Webkit;
using Java.Interop;

namespace Hermes.Mobile.Android.WebView;

internal sealed class JsBridge : Java.Lang.Object
{
    public const string Name = "HermesBridge";

    private readonly Action<string> _onMessage;
    private readonly Handler _handler = new(Looper.MainLooper!);

    public JsBridge(Action<string> onMessage)
    {
        _onMessage = onMessage;
    }

    [JavascriptInterface]
    [Export("postMessage")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Export is required for addJavascriptInterface bridge")]
    public void PostMessage(string message)
    {
        _handler.Post(() => _onMessage(message!));
    }
}
