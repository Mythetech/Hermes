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
/// <remarks>
/// Asset serving uses an embedded HTTP server on localhost instead of WKURLSchemeHandler.
/// The scheme handler approach is broken on .NET iOS due to a macios registrar regression
/// (dotnet/macios#23002). The JS↔C# bridge uses WKScriptMessageHandler which works
/// correctly with the static registrar + ProtocolAdoption workaround.
/// </remarks>
public sealed class HermesMobileHost : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly WKWebView _webView;
    private readonly IOSWebViewManager _manager;
    private readonly UIViewController _rootViewController;
    private readonly List<(Type Type, string Selector)> _rootComponents;
    private readonly EmbeddedFileServer _fileServer;

    // NSObject-bridged handlers must be rooted for the lifetime of the host; otherwise
    // the GC will collect them and native callbacks silently stop firing.
    private readonly ScriptMessageHandler _scriptHandler;
    private readonly AllowAllNavigationDelegate _navDelegate;

    private bool _started;

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    internal HermesMobileHost(
        IServiceProvider services,
        IFileProvider fileProvider,
        string hostPageRelativePath,
        IReadOnlyList<(Type Type, string Selector)> rootComponents)
    {
        _services = services;
        _rootComponents = new List<(Type, string)>(rootComponents);

        // Start embedded HTTP server to serve Blazor assets from the app bundle.
        // WKURLSchemeHandler is broken on .NET iOS, so we serve over localhost HTTP.
        _fileServer = EmbeddedFileServer.Start(fileProvider);
        var appBaseUri = new Uri($"{_fileServer.BaseUrl}/");

        var config = new WKWebViewConfiguration();
        config.AllowsInlineMediaPlayback = true;
        config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;

        var dispatcher = new Threading.IOSDispatcher();
        var jsComponents = new JSComponentConfigurationStore();

        // Script message handler for JS↔C# bridge (Blazor interop).
        // Late-bind to IOSWebViewManager to break the circular dependency:
        // handler needs manager, manager needs webview, webview needs config with handler.
        IOSWebViewManager? pendingManager = null;
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
        _fileServer.Dispose();
        _webView.Dispose();
    }
}
