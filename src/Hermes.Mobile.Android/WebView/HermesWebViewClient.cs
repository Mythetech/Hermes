// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.Webkit;

namespace Hermes.Mobile.Android.WebView;

internal sealed class HermesWebViewClient : WebViewClient
{
    private readonly Action _onPageFinished;
    private AndroidWebViewManager? _manager;

    public HermesWebViewClient(Action onPageFinished)
    {
        _onPageFinished = onPageFinished;
    }

    internal void SetManager(AndroidWebViewManager manager) => _manager = manager;

    public override bool ShouldOverrideUrlLoading(
        global::Android.Webkit.WebView? view, IWebResourceRequest? request)
    {
        if (request?.Url is null)
            return false;

        var url = request.Url.ToString();
        if (url is not null && url.StartsWith("https://0.0.0.0/", StringComparison.Ordinal))
            return true;

        return false;
    }

    public override WebResourceResponse? ShouldInterceptRequest(
        global::Android.Webkit.WebView? view, IWebResourceRequest? request)
    {
        if (request?.Url is null || _manager is null)
            return base.ShouldInterceptRequest(view, request);

        var url = request.Url.ToString();
        if (url is null)
            return base.ShouldInterceptRequest(view, request);

        var (statusCode, body, contentType) = _manager.ResolveRequest(url);
        if (statusCode == 200 && body.Length > 0)
        {
            return new WebResourceResponse(
                contentType,
                "UTF-8",
                statusCode,
                "OK",
                new Dictionary<string, string> { ["Cache-Control"] = "no-cache" },
                new MemoryStream(body));
        }

        return base.ShouldInterceptRequest(view, request);
    }

    public override void OnPageFinished(global::Android.Webkit.WebView? view, string? url)
    {
        base.OnPageFinished(view, url);
        _onPageFinished();
    }

    public override void OnReceivedError(
        global::Android.Webkit.WebView? view, IWebResourceRequest? request, WebResourceError? error)
    {
        Console.WriteLine($"[Hermes.Mobile.Android] WebView error: {error?.Description} (code {error?.ErrorCode})");
        base.OnReceivedError(view, request, error);
    }
}
