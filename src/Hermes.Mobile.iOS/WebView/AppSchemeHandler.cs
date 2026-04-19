// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Globalization;
using System.Runtime.Versioning;
using Foundation;
using Hermes.Mobile.WebView;
using ObjCRuntime;
using WebKit;

namespace Hermes.Mobile.iOS.WebView;

/// <summary>
/// Handles app:// requests by delegating to a resolver that wraps WebViewManager.TryGetResponseContent.
/// iOS runs scheme handler callbacks on the main thread.
/// </summary>
[Register("HermesAppSchemeHandler")]
[Adopts("WKURLSchemeHandler")]
internal sealed class AppSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    static AppSchemeHandler()
        => ProtocolAdoption.Ensure<AppSchemeHandler>("WKURLSchemeHandler");

    private readonly Func<string, WebViewResponse> _resolver;

    public AppSchemeHandler(Func<string, WebViewResponse> resolver)
    {
        _resolver = resolver;
    }

    [Export("webView:startURLSchemeTask:")]
    [SupportedOSPlatform("ios11.0")]
    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        var url = urlSchemeTask.Request.Url?.AbsoluteString;
        if (string.IsNullOrEmpty(url))
            return;

        var response = _resolver(url);

        if (response.StatusCode == 200)
        {
            using var headers = new NSMutableDictionary<NSString, NSString>();
            headers.Add((NSString)"Content-Length", (NSString)response.Body.Length.ToString(CultureInfo.InvariantCulture));
            headers.Add((NSString)"Content-Type", (NSString)response.ContentType);
            headers.Add((NSString)"Cache-Control", (NSString)"no-cache, max-age=0, must-revalidate, no-store");

            using var httpResponse = new NSHttpUrlResponse(urlSchemeTask.Request.Url!, response.StatusCode, "HTTP/1.1", headers);
            urlSchemeTask.DidReceiveResponse(httpResponse);
            urlSchemeTask.DidReceiveData(NSData.FromArray(response.Body));
            urlSchemeTask.DidFinish();
        }
        else
        {
            using var httpResponse = new NSHttpUrlResponse(urlSchemeTask.Request.Url!, response.StatusCode, "HTTP/1.1", null);
            urlSchemeTask.DidReceiveResponse(httpResponse);
            urlSchemeTask.DidFinish();
        }
    }

    [Export("webView:stopURLSchemeTask:")]
    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        // No-op. Do NOT touch urlSchemeTask after DidFinish has been called upstream.
    }
}
