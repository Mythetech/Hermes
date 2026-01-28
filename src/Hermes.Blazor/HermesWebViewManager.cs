using System.Buffers;
using System.Threading.Channels;
using Hermes.Abstractions;
using Hermes.Blazor.Diagnostics;
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
    private readonly Channel<string> _messageChannel;
    private readonly Task _messagePumpTask;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _disposed;

    public HermesWebViewManager(
        IHermesWindowBackend backend,
        IServiceProvider services,
        HermesDispatcher dispatcher,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        string hostPageRelativePath)
        : base(services, dispatcher, new Uri(AppBaseUri), fileProvider, jsComponents, hostPageRelativePath)
    {
        _backend = backend;

        _messageChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

        _messagePumpTask = RunMessagePumpAsync(_cts.Token);
        _backend.WebMessageReceived += OnWebMessageReceived;

        var scheme = new Uri(AppBaseUri).Scheme;
        _backend.RegisterCustomScheme(scheme, HandleWebRequest);
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        StartupLog.Log("WebView", $"NavigateCore: {absoluteUri}");
        _backend.NavigateToUrl(absoluteUri.ToString());
    }

    protected override void SendMessage(string message)
    {
        if (_disposed)
            return;

        if (!_messageChannel.Writer.TryWrite(message))
            _ = WriteMessageAsync(message);
    }

    private async Task WriteMessageAsync(string message)
    {
        try
        {
            await _messageChannel.Writer.WriteAsync(message, _cts.Token);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
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
        StartupLog.LogFirstMessage();
        MessageReceived(new Uri(AppBaseUri), message);
    }

    private Stream? HandleWebRequest(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;

        if (path.Contains("blazor.web.js") || path.Contains("aspnetcore-browser-refresh.js"))
            return null;

        var hasFileExtension = path.LastIndexOf('.') > path.LastIndexOf('/');
        var allowFallbackOnHostPage = !hasFileExtension;

        if (TryGetResponseContent(url, allowFallbackOnHostPage, out var statusCode, out var statusMessage,
            out var content, out var headers))
        {
            if (path == "/" || path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
                StartupLog.Log("WebView", "Serving index.html (host page)");
            else if (path.Contains("blazor.webview.js"))
                StartupLog.Log("WebView", "Serving blazor.webview.js");

            return content;
        }

        return null;
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
