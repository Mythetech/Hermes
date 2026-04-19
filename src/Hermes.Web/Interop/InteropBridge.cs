// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Hermes.Abstractions;

namespace Hermes.Web.Interop;

public sealed class InteropBridge
{
    private readonly IHermesWindowBackend _backend;
    private readonly Dictionary<string, Func<object?[], Task<object?>>> _invokeHandlers;
    private readonly Dictionary<string, List<Action<object?>>> _eventHandlers;

    internal InteropBridge(
        IHermesWindowBackend backend,
        InteropBridgeOptions options)
    {
        _backend = backend;
        _invokeHandlers = new Dictionary<string, Func<object?[], Task<object?>>>(options.InvokeHandlers);
        _eventHandlers = new Dictionary<string, List<Action<object?>>>(options.EventHandlers);

        _backend.WebMessageReceived += OnWebMessageReceived;
    }

    public void Send(string eventName, object? data = null)
    {
        var json = JsonSerializer.Serialize(
            new EventEnvelope { Name = eventName, Data = data },
            InteropJsonContext.Default.EventEnvelope);
        _backend.SendWebMessage(json);
    }

    internal void Detach()
    {
        _backend.WebMessageReceived -= OnWebMessageReceived;
    }

    private async void OnWebMessageReceived(string message)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize(message, InteropJsonContext.Default.InteropEnvelope);
            if (envelope is null) return;

            switch (envelope.Type)
            {
                case "invoke":
                    await HandleInvokeAsync(envelope);
                    break;
                case "event":
                    HandleEvent(envelope);
                    break;
            }
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[Hermes.Web] Ignoring non-bridge message: {ex.Message}");
        }
    }

    private async Task HandleInvokeAsync(InteropEnvelope envelope)
    {
        var id = envelope.Id ?? "";
        var method = envelope.Method ?? "";

        if (!_invokeHandlers.TryGetValue(method, out var handler))
        {
            SendError(id, $"Method not found: {method}");
            return;
        }

        try
        {
            var args = envelope.Args?.Select(e => (object?)e).ToArray() ?? [];
            var result = await handler(args);
            SendResult(id, result);
        }
        catch (Exception ex)
        {
            SendError(id, ex.Message);
        }
    }

    private void HandleEvent(InteropEnvelope envelope)
    {
        var name = envelope.Name ?? "";
        if (!_eventHandlers.TryGetValue(name, out var handlers))
            return;

        object? data = envelope.Data?.ValueKind == JsonValueKind.Undefined ? null : envelope.Data;

        foreach (var handler in handlers)
        {
            try
            {
                handler(data);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Hermes.Web] Event handler error for '{name}': {ex.Message}");
            }
        }
    }

    private void SendResult(string id, object? value)
    {
        var json = JsonSerializer.Serialize(
            new ResultEnvelope { Id = id, Value = value },
            InteropJsonContext.Default.ResultEnvelope);
        _backend.SendWebMessage(json);
    }

    private void SendError(string id, string message)
    {
        var json = JsonSerializer.Serialize(
            new ErrorEnvelope { Id = id, Message = message },
            InteropJsonContext.Default.ErrorEnvelope);
        _backend.SendWebMessage(json);
    }

    [RequiresUnreferencedCode("Handler argument types must be preserved by the consuming application")]
    [RequiresDynamicCode("Handler argument deserialization may require runtime code generation")]
    internal static TArg DeserializeArg<TArg>(object?[] args, int index)
    {
        if (index >= args.Length)
            throw new ArgumentException($"Expected argument at index {index} but only {args.Length} args provided");

        var raw = args[index];

        if (raw is JsonElement element)
            return element.Deserialize<TArg>()!;

        if (raw is TArg typed)
            return typed;

        return (TArg)Convert.ChangeType(raw!, typeof(TArg))!;
    }

    [RequiresUnreferencedCode("Event data types must be preserved by the consuming application")]
    [RequiresDynamicCode("Event data deserialization may require runtime code generation")]
    internal static T DeserializeData<T>(object? data)
    {
        if (data is JsonElement element)
            return element.Deserialize<T>()!;

        if (data is T typed)
            return typed;

        return (T)Convert.ChangeType(data!, typeof(T))!;
    }
}
