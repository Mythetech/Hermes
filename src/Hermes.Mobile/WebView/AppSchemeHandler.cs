// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Globalization;
using System.Runtime.Versioning;
using Foundation;
using ObjCRuntime;
using WebKit;

namespace Hermes.Mobile.WebView;

/// <summary>
/// Handles app:// requests by delegating to a resolver that wraps WebViewManager.TryGetResponseContent.
/// iOS runs scheme handler callbacks on the main thread.
/// </summary>
[Register("HermesAppSchemeHandler")]
[Adopts("WKURLSchemeHandler")]
internal sealed class AppSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    private readonly Func<string, (int StatusCode, byte[] Body, string ContentType)> _resolver;

    public AppSchemeHandler(Func<string, (int, byte[], string)> resolver)
    {
        Console.WriteLine("[Hermes.Mobile] AppSchemeHandler constructed");
        _resolver = resolver;
    }

    [Export("webView:startURLSchemeTask:")]
    [SupportedOSPlatform("ios11.0")]
    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        var url = urlSchemeTask.Request.Url?.AbsoluteString;
        if (string.IsNullOrEmpty(url))
        {
            Console.WriteLine("[Hermes.Mobile] scheme handler: empty URL, ignoring");
            return;
        }

        var (statusCode, body, contentType) = _resolver(url);
        Console.WriteLine($"[Hermes.Mobile] scheme handler: {url} → {statusCode} ({contentType}, {body.Length} bytes)");

        if (statusCode == 200)
        {
            using var headers = new NSMutableDictionary<NSString, NSString>();
            headers.Add((NSString)"Content-Length", (NSString)body.Length.ToString(CultureInfo.InvariantCulture));
            headers.Add((NSString)"Content-Type", (NSString)contentType);
            headers.Add((NSString)"Cache-Control", (NSString)"no-cache, max-age=0, must-revalidate, no-store");

            using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url!, statusCode, "HTTP/1.1", headers);
            urlSchemeTask.DidReceiveResponse(response);
            urlSchemeTask.DidReceiveData(NSData.FromArray(body));
            urlSchemeTask.DidFinish();
        }
        else
        {
            using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url!, statusCode, "HTTP/1.1", null);
            urlSchemeTask.DidReceiveResponse(response);
            urlSchemeTask.DidFinish();
        }
    }

    [Export("webView:stopURLSchemeTask:")]
    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        // No-op. Do NOT touch urlSchemeTask after DidFinish has been called upstream.
    }
}
