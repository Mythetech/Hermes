using System.Buffers;
using System.Threading.Channels;
using Hermes.Abstractions;
using Hermes.Blazor.Threading;
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
    internal static string AppBaseUri => OperatingSystem.IsWindows()
        ? "http://0.0.0.0/"
        : "app://0.0.0.0/";

    private readonly IHermesWindowBackend _backend;
    private readonly Channel<string> _messageChannel;
    private readonly Task _messagePumpTask;
    private readonly CancellationTokenSource _cts = new();

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

        // Bounded channel with async wait instead of Thread.Sleep
        _messageChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

        // Start message pump
        _messagePumpTask = RunMessagePumpAsync(_cts.Token);

        // Register for web messages from JS
        _backend.WebMessageReceived += OnWebMessageReceived;

        // Register custom scheme for Blazor resources
        var scheme = new Uri(AppBaseUri).Scheme;
        _backend.RegisterCustomScheme(scheme, HandleWebRequest);
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        _backend.NavigateToUrl(absoluteUri.ToString());
    }

    protected override void SendMessage(string message)
    {
        // Non-blocking write - if channel is full, wait asynchronously
        if (!_messageChannel.Writer.TryWrite(message))
        {
            // Slow path: queue the write
            _ = _messageChannel.Writer.WriteAsync(message, _cts.Token);
        }
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
                // Batch read for efficiency
                var count = 0;
                while (count < BatchSize && reader.TryRead(out var message))
                {
                    batch[count++] = message;
                }

                if (count > 0)
                {
                    // Send batch on UI thread
                    var messages = batch.AsSpan(0, count).ToArray();
                    _backend.BeginInvoke(() =>
                    {
                        foreach (var msg in messages)
                        {
                            _backend.SendWebMessage(msg);
                        }
                    });

                    // Clear references
                    Array.Clear(batch, 0, count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private void OnWebMessageReceived(string message)
    {
        // Route message to Blazor
        MessageReceived(new Uri(AppBaseUri), message);
    }

    private Stream? HandleWebRequest(string url)
    {
        // Parse the URL and get the relative path
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var relativePath = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(relativePath))
            relativePath = "index.html";

        // Try to get the response content
        if (TryGetResponseContent(relativePath, false, out var statusCode, out var statusMessage,
            out var content, out var headers))
        {
            return content;
        }

        return null;
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _cts.Cancel();
        _messageChannel.Writer.TryComplete();

        try
        {
            _messagePumpTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch { }

        _backend.WebMessageReceived -= OnWebMessageReceived;
        _cts.Dispose();

        return base.DisposeAsyncCore();
    }
}
