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

    // NSObject-bridged handlers must be rooted for the lifetime of the host; otherwise
    // the GC will collect them and native callbacks silently stop firing.
    private readonly AppSchemeHandler _schemeHandler;
    private readonly ScriptMessageHandler _scriptHandler;
    private readonly AllowAllNavigationDelegate _navDelegate;

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

        // All scheme handlers, user content controllers, and user scripts MUST be registered
        // on the config BEFORE creating the WKWebView — WKWebView copies the config at
        // construction and later mutations are ignored.

        // Scheme handler needs to delegate to IOSWebViewManager, which needs the WKWebView
        // itself (circular). Use a late-bound resolver that captures _manager after assignment.
        IOSWebViewManager? pendingManager = null;
        _schemeHandler = new AppSchemeHandler(url =>
            pendingManager?.ResolveRequest(url) ?? (404, Array.Empty<byte>(), string.Empty));
        config.SetUrlSchemeHandler(_schemeHandler, urlScheme: appBaseUri.Scheme);

        // Script message handler — same late-bind pattern.
        _scriptHandler = new ScriptMessageHandler(appBaseUri, (uri, msg) =>
            pendingManager?.MessageReceivedInternal(uri, msg));
        config.UserContentController.AddScriptMessageHandler(_scriptHandler, ScriptMessageHandler.Name);

        using var scriptSource = new NSString(BlazorInitScript.Contents);
        var userScript = new WKUserScript(scriptSource, WKUserScriptInjectionTime.AtDocumentEnd, isForMainFrameOnly: true);
        config.UserContentController.AddUserScript(userScript);

        _webView = new WKWebView(CGRect.Empty, config) { AutosizesSubviews = true };
        _webView.ScrollView.Bounces = false;

        _navDelegate = new AllowAllNavigationDelegate();
        _webView.NavigationDelegate = _navDelegate;

        _manager = new IOSWebViewManager(
            _webView, services, dispatcher, appBaseUri, fileProvider, jsComponents, hostPageRelativePath);
        pendingManager = _manager;

        // Always-on for PoC. iOS 16.4+ only.
        if (OperatingSystem.IsIOSVersionAtLeast(16, 4))
        {
            _webView.SetValueForKey(NSObject.FromObject(true), (NSString)"inspectable");
            Console.WriteLine("[Hermes.Mobile] webview inspectable = true");
        }
        else
        {
            Console.WriteLine("[Hermes.Mobile] iOS < 16.4; webview not inspectable");
        }

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
