// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.Webkit;
using Hermes.Mobile.WebView;

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

        var response = _manager.ResolveRequest(url);
        if (response.StatusCode == 200 && response.Body.Length > 0)
        {
            return new WebResourceResponse(
                response.ContentType,
                "UTF-8",
                response.StatusCode,
                "OK",
                new Dictionary<string, string> { ["Cache-Control"] = "no-cache" },
                new MemoryStream(response.Body));
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
