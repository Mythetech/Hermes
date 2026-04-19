// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using Hermes.Mobile.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using WebKit;

namespace Hermes.Mobile.iOS.WebView;

/// <summary>
/// WebViewManager subclass that binds a WKWebView's navigation + messaging to the Blazor pipeline.
/// </summary>
internal sealed class IOSWebViewManager : WebViewManager
{
    private readonly WKWebView _webView;
    private readonly Uri _appBaseUri;

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    public IOSWebViewManager(
        WKWebView webView,
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
        using var url = new Foundation.NSUrl(absoluteUri.ToString());
        using var request = new Foundation.NSUrlRequest(url);
        _webView.LoadRequest(request);
    }

    protected override void SendMessage(string message)
    {
        var encoded = JavaScriptEncoder.Default.Encode(message);
        _webView.EvaluateJavaScript(
            $"__dispatchMessageCallback(\"{encoded}\")",
            (result, error) =>
            {
                if (error is not null)
                    Console.WriteLine($"[Hermes.Mobile] C#→JS EvaluateJavaScript error: {error.LocalizedDescription}");
            });
    }

    /// <summary>Public pathway for the ScriptMessageHandler to feed JS→C# messages into the base pump.</summary>
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
