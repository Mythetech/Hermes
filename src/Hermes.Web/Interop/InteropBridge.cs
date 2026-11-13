// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using System.Threading.Channels;
using Hermes.Abstractions;
using Hermes.Diagnostics;

namespace Hermes.Web.Interop;

/// <summary>
/// Carries calls and events between C# and the page. Handlers run on the thread pool, never the UI thread;
/// replies and events reach the page through the UI thread, so every member can be used from any thread.
/// </summary>
public sealed partial class InteropBridge
{
    private readonly IHermesWindowBackend _backend;
    private readonly bool _useReflection;
    private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<InteropEnvelope> _eventQueue =
        Channel.CreateUnbounded<InteropEnvelope>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _closing = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, MethodHandler> _methods = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<EventSubscription>> _events = new(StringComparer.Ordinal);
    private readonly List<Task> _calls = [];
    private Task? _pump;
    private Task? _eventLoop;
    private bool _stopped;

    internal InteropBridge(IHermesWindowBackend backend)
        : this(backend, JsonSerializer.IsReflectionEnabledByDefault)
    {
    }

    internal InteropBridge(IHermesWindowBackend backend, bool useReflection)
    {
        _backend = backend;
        _useReflection = useReflection;
        SerializerOptions = BridgeJson.CreateOptions();
        _backend.WebMessageReceived += OnWebMessageReceived;
        _backend.Closing += OnClosing;
    }

    /// <summary>
    /// Serializes payloads in both directions, with web defaults (camelCase). Trimmed and native AOT apps add a
    /// JsonSerializerContext for their payload types to <see cref="JsonSerializerOptions.TypeInfoResolverChain"/> before the app runs and before the first Send.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; }

    internal TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Raised on the bridge's reader when the page has rendered, once per page load.</summary>
    internal event Action? PageReady;

    /// <summary>Raised on the bridge's reader for an error the page did not handle.</summary>
    internal event Action<PageError>? PageErrorReported;

    internal void Start()
    {
        lock (_gate)
        {
            if (_pump is not null || _stopped)
                return;

            BridgeJson.Freeze(SerializerOptions, _useReflection);

            // Both loops are joined in Stop, so neither is fire-and-forget.
            _pump = Task.Run(() => PumpAsync(_closing.Token));
            _eventLoop = Task.Run(() => EventLoopAsync(_closing.Token));
        }
    }

    internal void Stop()
    {
        Task[] pending;
        lock (_gate)
        {
            if (_stopped)
                return;

            _stopped = true;
            _backend.WebMessageReceived -= OnWebMessageReceived;
            _backend.Closing -= OnClosing;
            pending = _pump is null || _eventLoop is null ? [.. _calls] : [_pump, _eventLoop, .. _calls];
        }

        _incoming.Writer.TryComplete();
        _eventQueue.Writer.TryComplete();

        // Cancelling on the thread pool keeps handlers that resume on cancellation off the UI thread.
        pending = [.. pending, Task.Run(CancelHandlers)];

        // Blocking join: Stop runs after the message loop has ended, where an await could not resume here.
        try
        {
            if (!Task.WaitAll(pending, StopTimeout))
                HermesLogger.Error($"Bridge handlers did not finish within {StopTimeout.TotalSeconds:0.##}s; continuing teardown.");
        }
        catch (AggregateException ex)
        {
            HermesLogger.Error("A bridge handler failed while the bridge was stopping", ex);
        }
    }

    private void OnWebMessageReceived(string message) => _incoming.Writer.TryWrite(message);

    private void OnClosing() => ThreadPool.QueueUserWorkItem(static bridge => bridge.CancelHandlers(), this, preferLocal: false);

    private void CancelHandlers()
    {
        try
        {
            _closing.Cancel();
        }
        catch (AggregateException ex)
        {
            HermesLogger.Error("A cancellation callback registered by a bridge handler failed", ex);
        }
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _incoming.Reader.ReadAllAsync(cancellationToken))
            {
                // The reader hands over buffered messages without checking the token.
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (!TryParse(message, out var envelope))
                    continue;

                switch (envelope.Type)
                {
                    case "invoke":
                        StartCall(envelope, cancellationToken);
                        break;
                    case "event":
                        _eventQueue.Writer.TryWrite(envelope);
                        break;
                    case "ready":
                        Raise(PageReady, "the page's ready signal");
                        break;
                    case "page-error":
                        var error = new PageError(envelope.Source ?? "error", envelope.Name ?? "Error", envelope.Message ?? "", envelope.Stack);
                        Raise(() => PageErrorReported?.Invoke(error), "an error from the page");
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            HermesLogger.Error("The bridge stopped reading messages from the page", ex);
        }
        finally
        {
            _eventQueue.Writer.TryComplete();
        }
    }

    private static void Raise(Action? handler, string what)
    {
        try
        {
            handler?.Invoke();
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Handling {what} failed", ex);
        }
    }

    // Events wait for each other here, so calls from the page never wait behind an event handler.
    private async Task EventLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in _eventQueue.Reader.ReadAllAsync(cancellationToken))
            {
                if (IsStopping(cancellationToken))
                    break;

                await DispatchEventAsync(envelope, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            HermesLogger.Error("The bridge stopped handling events from the page", ex);
        }
    }

    private static bool TryParse(string message, out InteropEnvelope envelope)
    {
        envelope = null!;
        if (message.Length == 0 || message[0] != '{')
            return false;

        try
        {
            envelope = JsonSerializer.Deserialize(message, InteropJsonContext.Default.InteropEnvelope)!;
            return envelope is not null;
        }
        catch (Exception)
        {
            // Anything the reader rejects, such as invalid UTF-16, is not a bridge message.
            return false;
        }
    }

    private void StartCall(InteropEnvelope envelope, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_stopped)
                return;

            _calls.RemoveAll(static call => call.IsCompleted);

            // Each call observes its own failures and is joined in Stop, so none is fire-and-forget.
            _calls.Add(Task.Run(() => RunCallAsync(envelope, cancellationToken)));
        }
    }

    private async Task RunCallAsync(InteropEnvelope envelope, CancellationToken cancellationToken)
    {
        var id = envelope.Id;
        if (string.IsNullOrEmpty(id))
            return;

        var method = envelope.Method ?? "";
        try
        {
            MethodHandler? handler;
            lock (_gate)
                _methods.TryGetValue(method, out handler);

            if (handler is null)
            {
                Post(BridgeJson.Error(id, $"Method not found: {method}"));
                return;
            }

            var args = envelope.Args ?? [];
            if (args.Length != handler.ParameterTypes.Length)
            {
                Post(BridgeJson.Error(id, $"'{method}' takes {handler.ParameterTypes.Length} argument(s) but was called with {args.Length}"));
                return;
            }

            var values = new object?[args.Length];
            for (var i = 0; i < args.Length; i++)
                values[i] = BridgeJson.Read(args[i], BridgeJson.GetTypeInfo(SerializerOptions, handler.ParameterTypes[i]));

            var result = await handler.Invoke(values, cancellationToken);
            var resultType = handler.ResultType is null ? null : BridgeJson.GetTypeInfo(SerializerOptions, handler.ResultType);
            Post(BridgeJson.Result(id, result, resultType));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The window closed, so there is no page left to reply to.
        }
        catch (Exception ex)
        {
            // The page gets the error as a rejected promise and decides whether it is a failure.
            HermesLogger.Warning($"Bridge method '{method}' failed: {ex}");
            Post(BridgeJson.Error(id, ex.Message));
        }
    }

    private async Task DispatchEventAsync(InteropEnvelope envelope, CancellationToken cancellationToken)
    {
        var name = envelope.Name ?? "";
        EventSubscription[] subscriptions;
        lock (_gate)
        {
            if (!_events.TryGetValue(name, out var list))
                return;
            subscriptions = [.. list];
        }

        foreach (var subscription in subscriptions)
        {
            if (IsStopping(cancellationToken))
                return;

            try
            {
                var data = subscription.DataType is null
                    ? null
                    : BridgeJson.Read(envelope.Data, BridgeJson.GetTypeInfo(SerializerOptions, subscription.DataType));
                await subscription.Invoke(data);
            }
            catch (Exception ex)
            {
                HermesLogger.Error($"Bridge event handler for '{name}' failed", ex);
            }
        }
    }

    // Stop sets the flag before it returns; the cancellation it queues on the thread pool can come later.
    private bool IsStopping(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested || Volatile.Read(ref _stopped);

    private void Post(string json)
    {
        if (Volatile.Read(ref _stopped))
            return;

        try
        {
            _backend.BeginInvoke(() => SendToPage(json));
        }
        catch (Exception ex)
        {
            HermesLogger.Error("The bridge could not reach the UI thread", ex);
        }
    }

    private void SendToPage(string json)
    {
        try
        {
            _backend.SendWebMessage(json);
        }
        catch (Exception ex)
        {
            HermesLogger.Error("The bridge could not send a message to the page", ex);
        }
    }
}
