// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Android.Views;
using Hermes.Mobile.Android.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Mobile.Android;

public sealed class HermesMobileAndroidHost : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly global::Android.Webkit.WebView _webView;
    private readonly AndroidWebViewManager _manager;
    private readonly List<(Type Type, string Selector)> _rootComponents;

    private bool _started;

    private static readonly Uri AppBaseUri = new("https://0.0.0.0/");

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    internal HermesMobileAndroidHost(
        Context context,
        IServiceProvider services,
        IFileProvider fileProvider,
        string hostPageRelativePath,
        IReadOnlyList<(Type Type, string Selector)> rootComponents)
    {
        _services = services;
        _rootComponents = new List<(Type, string)>(rootComponents);

        _webView = new global::Android.Webkit.WebView(context);
        var settings = _webView.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.AllowFileAccess = true;

#if DEBUG
        global::Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);
#endif

        var dispatcher = new Threading.AndroidDispatcher();
        var jsComponents = new JSComponentConfigurationStore();

        _manager = new AndroidWebViewManager(
            _webView, services, dispatcher, AppBaseUri, fileProvider, jsComponents, hostPageRelativePath);

        var bridge = new JsBridge(message =>
            _manager.MessageReceivedInternal(AppBaseUri, message));
        _webView.AddJavascriptInterface(bridge, JsBridge.Name);

        var webViewClient = new HermesWebViewClient(OnPageFinished);
        webViewClient.SetManager(_manager);
        _webView.SetWebViewClient(webViewClient);
        _webView.SetWebChromeClient(new HermesWebChromeClient());
    }

    public View RootView => _webView;

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

    private void OnPageFinished()
    {
        _webView.EvaluateJavascript(BlazorInitScript.Contents, null);
    }

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
        _webView.Dispose();
    }
}
