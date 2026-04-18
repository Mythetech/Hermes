// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using CoreGraphics;
using Foundation;
using Hermes.Mobile.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using UIKit;
using WebKit;

namespace Hermes.Mobile;

/// <summary>
/// Hosts a Blazor app inside a WKWebView embedded in a UIViewController.
/// The iOS AppDelegate places the RootViewController into its UIWindow.
/// </summary>
public sealed class HermesMobileHost : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly WKWebView _webView;
    private readonly IOSWebViewManager _manager;
    private readonly UIViewController _rootViewController;
    private readonly List<(Type Type, string Selector)> _rootComponents;
    private bool _started;

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    internal HermesMobileHost(
        IServiceProvider services,
        IFileProvider fileProvider,
        Uri appBaseUri,
        string hostPageRelativePath,
        IReadOnlyList<(Type Type, string Selector)> rootComponents)
    {
        _services = services;
        _rootComponents = new List<(Type, string)>(rootComponents);

        var config = new WKWebViewConfiguration();
        config.AllowsInlineMediaPlayback = true;
        config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;

        var dispatcher = new Threading.IOSDispatcher();
        var jsComponents = new JSComponentConfigurationStore();

        _webView = new WKWebView(CGRect.Empty, config) { AutosizesSubviews = true };
        _webView.ScrollView.Bounces = false;

        _manager = new IOSWebViewManager(
            _webView, services, dispatcher, appBaseUri, fileProvider, jsComponents, hostPageRelativePath);

        var schemeHandler = new AppSchemeHandler(_manager.ResolveRequest);
        config.SetUrlSchemeHandler(schemeHandler, urlScheme: appBaseUri.Scheme);

        // Wire JS→C# bridge. The IOSWebViewManager exposes an internal helper that forwards
        // to the protected WebViewManager.MessageReceived base method.
        var scriptHandler = new ScriptMessageHandler(appBaseUri, _manager.MessageReceivedInternal);
        config.UserContentController.AddScriptMessageHandler(scriptHandler, ScriptMessageHandler.Name);

        using var scriptSource = new NSString(BlazorInitScript.Contents);
        var userScript = new WKUserScript(scriptSource, WKUserScriptInjectionTime.AtDocumentEnd, isForMainFrameOnly: true);
        config.UserContentController.AddUserScript(userScript);

#if DEBUG
        if (OperatingSystem.IsIOSVersionAtLeast(16, 4))
        {
            _webView.SetValueForKey(NSObject.FromObject(true), (NSString)"inspectable");
        }
#endif

        _rootViewController = new UIViewController();
        var rootView = _rootViewController.View!;
        rootView.BackgroundColor = UIColor.SystemBackground;
        rootView.AddSubview(_webView);

        _webView.TranslatesAutoresizingMaskIntoConstraints = false;
        _webView.TopAnchor.ConstraintEqualTo(rootView.SafeAreaLayoutGuide.TopAnchor).Active = true;
        _webView.BottomAnchor.ConstraintEqualTo(rootView.SafeAreaLayoutGuide.BottomAnchor).Active = true;
        _webView.LeadingAnchor.ConstraintEqualTo(rootView.SafeAreaLayoutGuide.LeadingAnchor).Active = true;
        _webView.TrailingAnchor.ConstraintEqualTo(rootView.SafeAreaLayoutGuide.TrailingAnchor).Active = true;
    }

    public UIViewController RootViewController => _rootViewController;

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    public void Start()
    {
        if (_started) return;
        _started = true;

        foreach (var (type, selector) in _rootComponents)
        {
            _manager.AddRootComponentAsync(type, selector, ParameterView.Empty).GetAwaiter().GetResult();
        }

        _manager.Navigate("/");
    }

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
        _webView.Dispose();
    }
}
