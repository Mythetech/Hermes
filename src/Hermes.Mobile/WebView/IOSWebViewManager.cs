// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using WebKit;

namespace Hermes.Mobile.WebView;

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
        // Custom URL scheme handlers don't fire in current .NET iOS bindings, so we load the
        // index.html directly from the app bundle via file:// and let Blazor's JS bridge
        // (window.webkit.messageHandlers) take over from there. Static assets (blazor.webview.js,
        // _content/Shared.App/*) resolve via relative paths in the HTML.
        var bundleRoot = Foundation.NSBundle.MainBundle.ResourcePath!;
        var indexPath = Path.Combine(bundleRoot, "wwwroot", "index.html");
        var readAccessDir = Path.Combine(bundleRoot, "wwwroot");

        Console.WriteLine($"[Hermes.Mobile] NavigateCore: file={indexPath}, readAccess={readAccessDir}");
        using var fileUrl = Foundation.NSUrl.FromFilename(indexPath);
        using var readUrl = Foundation.NSUrl.FromFilename(readAccessDir);
        _webView.LoadFileUrl(fileUrl, readUrl);
    }

    protected override void SendMessage(string message)
    {
        var encoded = JavaScriptEncoder.Default.Encode(message);
        Console.WriteLine($"[Hermes.Mobile] C#→JS SendMessage ({message.Length} chars)");
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

    internal (int StatusCode, byte[] Body, string ContentType) ResolveRequest(string absoluteUrl)
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
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            content.Dispose();

            var contentType = headers.TryGetValue("Content-Type", out var ct)
                ? ct
                : MimeTypeLookup.GetContentType(absoluteUrl);

            return (200, ms.ToArray(), contentType);
        }

        return (404, Array.Empty<byte>(), string.Empty);
    }
}
