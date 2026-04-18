// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Foundation;
using ObjCRuntime;
using WebKit;

namespace Hermes.Mobile.WebView;

/// <summary>
/// Minimal WKNavigationDelegate that allows all navigation. Without an explicit delegate
/// WKWebView's default policy rejects custom-scheme navigation with requestURLIsValid=0,
/// producing a blank webview.
/// </summary>
[Adopts("WKNavigationDelegate")]
internal sealed class AllowAllNavigationDelegate : NSObject, IWKNavigationDelegate
{
    static AllowAllNavigationDelegate()
        => ProtocolAdoption.Ensure<AllowAllNavigationDelegate>("WKNavigationDelegate");

    [Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
    public void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
    {
        decisionHandler(WKNavigationActionPolicy.Allow);
    }

    [Export("webView:didFailNavigation:withError:")]
    public void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
    {
        Console.WriteLine($"[Hermes.Mobile] didFailNavigation: {error.LocalizedDescription}");
    }

    [Export("webView:didFailProvisionalNavigation:withError:")]
    public void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
    {
        Console.WriteLine($"[Hermes.Mobile] didFailProvisionalNavigation: {error.LocalizedDescription}");
    }
}
