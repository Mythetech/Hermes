// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using System.Threading.Channels;
using Hermes.Blazor.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// Drives the real WebViewManager IPC pipeline with no native WebView: every outgoing
/// message is recorded, incoming messages are fed in as strings, and unhandled exceptions
/// are captured instead of raised on the application. This is the fixture stage 0 needed
/// to see the dropped attach; it is the only way to assert IPC behavior without a window.
/// </summary>
internal sealed class HeadlessWebViewHost : WebViewManager
{
    public static readonly Uri BaseUri = new("app://localhost/");

    private readonly Channel<bool> _sentSignal = Channel.CreateUnbounded<bool>();

    public List<string> Sent { get; } = new();

    public List<Uri> Navigations { get; } = new();

    public List<Exception> UnhandledExceptions { get; } = new();

    private HeadlessWebViewHost(IServiceProvider services)
        : base(services, Dispatcher.CreateDefault(), BaseUri, new NullFileProvider(), "index.html")
    {
    }

    public static HeadlessWebViewHost Create(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddHermesBlazorWebView();
        configure?.Invoke(services);
        return new HeadlessWebViewHost(services.BuildServiceProvider());
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        lock (Navigations)
        {
            Navigations.Add(absoluteUri);
        }
    }

    protected override void SendMessage(string message)
    {
        lock (Sent)
        {
            Sent.Add(message);
        }

        _sentSignal.Writer.TryWrite(true);
    }

    protected override void HandleUnhandledException(Exception exception)
    {
        lock (UnhandledExceptions)
        {
            UnhandledExceptions.Add(exception);
        }
    }

    public Task ReceiveAsync(string message) => MessageReceivedAsync(BaseUri, message);

    /// <summary>
    /// Returns the first sent message of the given type, waiting for it to arrive if needed.
    /// </summary>
    public Task<string> WaitForSentAsync(string messageType, TimeSpan? timeout = null)
        => WaitForSentStartingWithAsync($"__bwv:[\"{messageType}\"", timeout);

    /// <summary>
    /// Returns the first sent message that starts with the given text, waiting if needed.
    /// </summary>
    public async Task<string> WaitForSentStartingWithAsync(string prefix, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        var scanned = 0;
        while (true)
        {
            lock (Sent)
            {
                for (; scanned < Sent.Count; scanned++)
                {
                    if (Sent[scanned].StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return Sent[scanned];
                    }
                }
            }

            await _sentSignal.Reader.ReadAsync(cts.Token);
        }
    }

    /// <summary>
    /// Plays the page's side of startup: AttachPage, then the render acknowledgment for the
    /// first batch, which is what lets the attach task complete.
    /// </summary>
    public async Task AttachPageAsync()
    {
        var attach = ReceiveAsync("""__bwv:["AttachPage","app://localhost/","app://localhost/"]""");
        var batch = await WaitForSentAsync("RenderBatch");
        await AcknowledgeRenderBatchAsync(batch);
        await attach;
    }

    public async Task AcknowledgeRenderBatchAsync(string renderBatchMessage)
    {
        long batchId;
        using (var document = JsonDocument.Parse(renderBatchMessage.Substring(IpcMessageWriter.MessagePrefix.Length)))
        {
            batchId = document.RootElement[1].GetInt64();
        }

        await ReceiveAsync($"__bwv:[\"OnRenderCompleted\",{batchId},null]");

        // The renderer runs OnAfterRender as a continuation of the acknowledged batch on its
        // own synchronization context. Queueing an empty work item behind it guarantees that
        // continuation has run before the caller inspects component state.
        await Dispatcher.InvokeAsync(() => { });
    }

    public string[] SentSnapshot()
    {
        lock (Sent)
        {
            return Sent.ToArray();
        }
    }
}
