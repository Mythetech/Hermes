// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers;
using System.Threading.Channels;
using Hermes.Abstractions;
using Hermes.Blazor.Diagnostics;
using Hermes.Blazor.Startup;
using Hermes.Blazor.Threading;
using Hermes.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor;

/// <summary>
/// WebViewManager for Hermes with optimized message pump.
/// Uses bounded channel instead of blocking Thread.Sleep.
/// </summary>
internal sealed class HermesWebViewManager : WebViewManager
{
    // Platform-specific base URIs
    // On Windows, we use http:// because WebView2 doesn't support custom schemes for top-level navigation
    // On Linux/Mac, we use app:// custom scheme because their webviews don't intercept http://
    public static string AppBaseUri => OperatingSystem.IsWindows()
        ? "http://localhost/"
        : "app://localhost/";

    private readonly IHermesWindowBackend _backend;
    private readonly Uri _baseUri;
    private readonly bool _isDevMode;
    private readonly InlinedHostPage? _hostPage;
    private readonly Channel<string> _messageChannel;
    private readonly Task _messagePumpTask;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _disposed;
    private int _firstMessageLogged;
    private int _firstRenderBatchLogged;

    public HermesWebViewManager(
        IHermesWindowBackend backend,
        IServiceProvider services,
        HermesDispatcher dispatcher,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        string hostPageRelativePath)
        : this(backend, services, dispatcher, fileProvider, jsComponents, hostPageRelativePath, baseUri: null, isDevMode: false)
    {
    }

    internal HermesWebViewManager(
        IHermesWindowBackend backend,
        IServiceProvider services,
        HermesDispatcher dispatcher,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        string hostPageRelativePath,
        string? baseUri,
        bool isDevMode,
        StartupSchemeHandler? startupHandler = null,
        InlinedHostPage? hostPage = null)
        : base(services, dispatcher, new Uri(baseUri ?? AppBaseUri), fileProvider, jsComponents, hostPageRelativePath)
    {
        _backend = backend;
        _baseUri = new Uri(baseUri ?? AppBaseUri);
        _isDevMode = isDevMode;
        _hostPage = hostPage;

        // Unbounded with SingleReader gets the runtime's zero-allocation
        // SingleConsumerUnboundedChannel; Blazor's render-batch acknowledgment
        // already provides upstream flow control, so a bound adds no safety
        _messageChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

        _messagePumpTask = RunMessagePumpAsync(_cts.Token);
        _backend.WebMessageReceived += OnWebMessageReceived;

        if (!_isDevMode)
        {
            // The builder registers the startup handler before the window exists so
            // early requests are served without the manager; installing the real
            // handler here switches every later request over. Direct construction
            // (tests, custom hosts) registers with the backend as before.
            if (startupHandler is not null)
                startupHandler.SetInner(HandleWebRequest);
            else
                _backend.RegisterCustomScheme(_baseUri.Scheme, HandleWebRequest);
        }
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        StartupLog.Phase("navigate");
        _backend.NavigateToUrl(absoluteUri.ToString());
    }

    protected override void SendMessage(string message)
    {
        if (_disposed)
            return;

        if (StartupLog.IsEnabled && message.StartsWith("__bwv:[\"RenderBatch\"", StringComparison.Ordinal))
            StartupLog.PhaseOnce("first-render-batch", ref _firstRenderBatchLogged);

        // Unbounded TryWrite only fails once the writer is completed during shutdown
        _messageChannel.Writer.TryWrite(message);
    }

    private async Task RunMessagePumpAsync(CancellationToken cancellationToken)
    {
        const int BatchSize = 16;
        var batch = new string[BatchSize];

        try
        {
            var reader = _messageChannel.Reader;

            while (await reader.WaitToReadAsync(cancellationToken))
            {
                var count = 0;
                while (count < BatchSize && reader.TryRead(out var message))
                {
                    batch[count++] = message;
                }

                if (count > 0)
                {
                    var messages = batch.AsSpan(0, count).ToArray();
                    _backend.BeginInvoke(() =>
                    {
                        foreach (var msg in messages)
                        {
                            _backend.SendWebMessage(msg);
                        }
                    });
                    Array.Clear(batch, 0, count);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnWebMessageReceived(string message)
    {
        StartupLog.PhaseOnce("first-message", ref _firstMessageLogged);
        MessageReceived(_baseUri, message);
    }

    /// <summary>
    /// Delivers a message that arrived before this manager existed, exactly as if
    /// the WebView had just sent it.
    /// </summary>
    internal void ReplayMessage(string message) => OnWebMessageReceived(message);

    private (Stream? Content, string? ContentType) HandleWebRequest(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;

        if (path.Contains("blazor.web.js") || path.Contains("aspnetcore-browser-refresh.js"))
            return (null, null);

        var hasFileExtension = path.LastIndexOf('.') > path.LastIndexOf('/');
        var allowFallbackOnHostPage = !hasFileExtension;

        // Strip query string - TryGetResponseContent determines Content-Type from URL,
        // and "file.js?v=1.0" would give wrong MIME type
        var cleanUrl = uri.GetLeftPart(UriPartial.Path);

        if (TryGetResponseContent(cleanUrl, allowFallbackOnHostPage, out var statusCode, out var statusMessage,
            out var content, out var headers))
        {
            headers.TryGetValue("Content-Type", out var contentType);

            if (contentType is not null && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                return (ServeHtmlWithInlinedScript(cleanUrl, path, content), contentType);

            return (content, contentType);
        }

        return (null, null);
    }

    private string? _inlinedPageUrl;
    private byte[]? _inlinedPageBytes;

    private Stream ServeHtmlWithInlinedScript(string cleanUrl, string path, Stream content)
    {
        // Prefer the page the startup handler already built so both handlers
        // serve identical bytes and the inlining work happens once per process.
        if (_hostPage is not null && _hostPage.Matches(path) && _hostPage.TryGetBytes(out var shared))
        {
            content.Dispose();
            return new MemoryStream(shared, writable: false);
        }

        if (_inlinedPageUrl != cleanUrl)
        {
            using var reader = new StreamReader(content);
            var html = reader.ReadToEnd();

            var inlined = HostPageInliner.Inline(html, asset =>
            {
                if (!TryGetResponseContent(_baseUri + asset, allowFallbackOnHostPage: false,
                    out _, out _, out var assetContent, out _))
                    return null;

                using var assetReader = new StreamReader(assetContent);
                return assetReader.ReadToEnd();
            });

            _inlinedPageBytes = System.Text.Encoding.UTF8.GetBytes(inlined);
            _inlinedPageUrl = cleanUrl;
        }
        else
        {
            content.Dispose();
        }

        return new MemoryStream(_inlinedPageBytes!);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _disposed = true;
        _cts.Cancel();
        _messageChannel.Writer.TryComplete();

        try
        {
            _messagePumpTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            HermesLogger.Warning($"Message pump task did not complete gracefully during dispose: {ex.Message}");
        }

        _backend.WebMessageReceived -= OnWebMessageReceived;
        _cts.Dispose();

        return base.DisposeAsyncCore();
    }
}
