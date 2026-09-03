// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;

namespace Hermes.Blazor.Startup;

/// <summary>
/// Records web messages that arrive before the WebViewManager exists, then replays
/// them in order once it does. Blazor's boot handshake starts with an AttachPage
/// message from the page script; with the WebView loading during composition that
/// message can land before there is anything to receive it.
/// </summary>
internal sealed class WebMessageBuffer : IDisposable
{
    private readonly IHermesWindowBackend _backend;
    private readonly List<string> _messages = new();
    private readonly object _lock = new();
    private bool _attached;

    public WebMessageBuffer(IHermesWindowBackend backend)
    {
        _backend = backend;
        _backend.WebMessageReceived += OnMessage;
        _attached = true;
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _messages.Count;
            }
        }
    }

    /// <summary>
    /// Stops buffering, then delivers every buffered message to the sink in
    /// arrival order. Detaching first means a message can never be both buffered
    /// and delivered live.
    /// </summary>
    public void Drain(Action<string> sink)
    {
        Detach();

        string[] snapshot;
        lock (_lock)
        {
            snapshot = _messages.ToArray();
            _messages.Clear();
        }

        foreach (var message in snapshot)
            sink(message);
    }

    public void Detach()
    {
        if (!_attached) return;
        _backend.WebMessageReceived -= OnMessage;
        _attached = false;
    }

    public void Dispose() => Detach();

    private void OnMessage(string message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
    }
}
