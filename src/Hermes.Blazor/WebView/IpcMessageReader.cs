// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Hermes.Blazor.WebView;

/// <summary>
/// The messages blazor.webview.js sends to the host. The names are the wire values.
/// </summary>
internal enum IncomingMessageType
{
    AttachPage,
    BeginInvokeDotNet,
    EndInvokeJS,
    OnRenderCompleted,
    OnLocationChanged,
    ReceiveByteArrayFromJS,
    OnLocationChanging,
}

/// <summary>
/// Parses incoming <c>__bwv:</c> envelopes with <see cref="JsonDocument"/> instead of the
/// serializer, so Native AOT needs no <c>JsonElement[]</c> converter. Unlike upstream, a
/// prefixed message that cannot be parsed throws instead of being treated as absent: the caller
/// surfaces it, which turns a blank window into a reported exception.
/// </summary>
internal static class IpcMessageReader
{
    public static bool TryRead(string? message, out IncomingMessageType messageType, out ArraySegment<JsonElement> args)
    {
        // Applications may share the web message channel for their own traffic, so anything
        // without the prefix is not ours and is ignored, exactly as upstream does.
        if (message is null || !message.StartsWith(IpcMessageWriter.MessagePrefix, StringComparison.Ordinal))
        {
            messageType = default;
            args = default;
            return false;
        }

        var payload = message.AsSpan(IpcMessageWriter.MessagePrefix.Length);
        var rented = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(payload.Length));
        try
        {
            var written = Encoding.UTF8.GetBytes(payload, rented);
            using var document = JsonDocument.Parse(rented.AsMemory(0, written));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 || root[0].ValueKind != JsonValueKind.String)
            {
                throw new JsonException("A Blazor WebView IPC message must be a JSON array whose first element is the message type name.");
            }

            var elements = new JsonElement[root.GetArrayLength()];
            var index = 0;
            foreach (var element in root.EnumerateArray())
            {
                // The document is disposed on return; clones own their data.
                elements[index++] = element.Clone();
            }

            var typeName = elements[0].GetString();
            messageType = typeName switch
            {
                "AttachPage" => IncomingMessageType.AttachPage,
                "BeginInvokeDotNet" => IncomingMessageType.BeginInvokeDotNet,
                "EndInvokeJS" => IncomingMessageType.EndInvokeJS,
                "OnRenderCompleted" => IncomingMessageType.OnRenderCompleted,
                "OnLocationChanged" => IncomingMessageType.OnLocationChanged,
                "ReceiveByteArrayFromJS" => IncomingMessageType.ReceiveByteArrayFromJS,
                "OnLocationChanging" => IncomingMessageType.OnLocationChanging,
                _ => throw new InvalidOperationException($"Unknown Blazor WebView IPC message type '{typeName}'."),
            };
            args = new ArraySegment<JsonElement>(elements, 1, elements.Length - 1);
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
