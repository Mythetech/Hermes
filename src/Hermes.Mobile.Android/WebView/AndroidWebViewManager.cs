// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using Android.OS;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;

using Hermes.Mobile.WebView;

namespace Hermes.Mobile.Android.WebView;

internal sealed class AndroidWebViewManager : WebViewManager
{
    private readonly global::Android.Webkit.WebView _webView;
    private readonly Uri _appBaseUri;
    private readonly Handler _handler = new(Looper.MainLooper!);

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    public AndroidWebViewManager(
        global::Android.Webkit.WebView webView,
        IServiceProvider services,
        Dispatcher dispatcher,
        Uri appBaseUri,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        string hostPageRelativePath)
        : base(services, dispatcher, appBaseUri, fileProvider, jsComponents, hostPageRelativePath)
    {
        _webView = webView;
        _appBaseUri = appBaseUri;
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        // Load the host page HTML directly via LoadDataWithBaseURL so the WebView
        // doesn't attempt a real network request to the synthetic origin.
        // Sub-resource requests (JS, CSS, etc.) go through ShouldInterceptRequest.
        if (TryGetResponseContent(
                absoluteUri.ToString(),
                allowFallbackOnHostPage: true,
                out _,
                out _,
                out var content,
                out _))
        {
            using var reader = new StreamReader(content);
            var html = reader.ReadToEnd();
            content.Dispose();

            _webView.LoadDataWithBaseURL(
                _appBaseUri.ToString(),
                html,
                "text/html",
                "UTF-8",
                null);
        }
    }

    protected override void SendMessage(string message)
    {
        var encoded = JavaScriptEncoder.Default.Encode(message);
        var js = $"__dispatchMessageCallback(\"{encoded}\")";

        if (Looper.MyLooper() == Looper.MainLooper)
        {
            _webView.EvaluateJavascript(js, null);
        }
        else
        {
            _handler.Post(() => _webView.EvaluateJavascript(js, null));
        }
    }

    internal void MessageReceivedInternal(Uri sourceUri, string message)
        => MessageReceived(sourceUri, message);

    internal WebViewResponse ResolveRequest(string absoluteUrl)
    {
        var allowFallbackOnHostPage = _appBaseUri.IsBaseOf(new Uri(absoluteUrl));

        if (TryGetResponseContent(
                absoluteUrl,
                allowFallbackOnHostPage,
                out var statusCode,
                out _,
                out var content,
                out var headers))
        {
            return WebViewResolveHelper.ToResponse(statusCode, content, headers, absoluteUrl);
        }

        return WebViewResponse.NotFound;
    }
}
