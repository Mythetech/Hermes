// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop.Infrastructure;

namespace Hermes.Blazor.WebView;

/// <summary>
/// Writes the outgoing <c>__bwv:</c> envelopes that blazor.webview.js consumes. Every field is
/// written directly with <see cref="Utf8JsonWriter"/>, so no argument is boxed into an
/// <c>object[]</c> and handed to a reflection-based serializer; that boxing is what Native AOT
/// could not serialize and what silently dropped the renderer interop attach. The output is
/// byte-identical to Microsoft.AspNetCore.Components.WebView 10.0.11 for every message type.
/// </summary>
internal static class IpcMessageWriter
{
    internal const string MessagePrefix = "__bwv:";

    private static readonly byte[] PrefixBytes = Encoding.UTF8.GetBytes(MessagePrefix);

    public static string RenderBatch(long batchId, ReadOnlySpan<byte> batchData)
    {
        var buffer = Begin("RenderBatch", out var writer);
        writer.WriteNumberValue(batchId);
        // Upstream base64-encodes into a string first, so '+' and '/' pass through the JSON
        // string escaper. WriteBase64StringValue would leave them unescaped and change the bytes.
        writer.WriteStringValue(Convert.ToBase64String(batchData));
        return End(buffer, writer);
    }

    public static string Navigate(string uri, NavigationOptions options)
    {
        var buffer = Begin("Navigate", out var writer);
        writer.WriteStringValue(uri);
        writer.WriteStartObject();
        writer.WriteBoolean("forceLoad", options.ForceLoad);
        writer.WriteBoolean("replaceHistoryEntry", options.ReplaceHistoryEntry);
        writer.WriteString("historyEntryState", options.HistoryEntryState);
        writer.WriteEndObject();
        return End(buffer, writer);
    }

    public static string Refresh(bool forceReload)
    {
        var buffer = Begin("Refresh", out var writer);
        writer.WriteBooleanValue(forceReload);
        return End(buffer, writer);
    }

    public static string AttachToDocument(int componentId, string selector)
    {
        var buffer = Begin("AttachToDocument", out var writer);
        writer.WriteNumberValue(componentId);
        writer.WriteStringValue(selector);
        return End(buffer, writer);
    }

    public static string BeginInvokeJS(in JSInvocationInfo invocationInfo)
    {
        var buffer = Begin("BeginInvokeJS", out var writer);
        writer.WriteNumberValue(invocationInfo.AsyncHandle);
        writer.WriteStringValue(invocationInfo.Identifier);
        writer.WriteStringValue(invocationInfo.ArgsJson);
        writer.WriteNumberValue((int)invocationInfo.ResultType);
        writer.WriteNumberValue(invocationInfo.TargetInstanceId);
        writer.WriteNumberValue((int)invocationInfo.CallType);
        return End(buffer, writer);
    }

    public static string EndInvokeDotNet(string callId, bool success, string? invocationResultOrError)
    {
        var buffer = Begin("EndInvokeDotNet", out var writer);
        writer.WriteStringValue(callId);
        writer.WriteBooleanValue(success);
        writer.WriteStringValue(invocationResultOrError);
        return End(buffer, writer);
    }

    public static string SendByteArrayToJS(int id, byte[] data)
    {
        var buffer = Begin("SendByteArrayToJS", out var writer);
        writer.WriteNumberValue(id);
        writer.WriteBase64StringValue(data);
        return End(buffer, writer);
    }

    public static string SetHasLocationChangingListeners(bool hasListeners)
    {
        var buffer = Begin("SetHasLocationChangingListeners", out var writer);
        writer.WriteBooleanValue(hasListeners);
        return End(buffer, writer);
    }

    public static string EndLocationChanging(int callId, bool shouldContinueNavigation)
    {
        var buffer = Begin("EndLocationChanging", out var writer);
        writer.WriteNumberValue(callId);
        writer.WriteBooleanValue(shouldContinueNavigation);
        return End(buffer, writer);
    }

    public static string NotifyUnhandledException(string message, string? stackTrace)
    {
        var buffer = Begin("NotifyUnhandledException", out var writer);
        writer.WriteStringValue(message);
        writer.WriteStringValue(stackTrace);
        return End(buffer, writer);
    }

    private static ArrayBufferWriter<byte> Begin(string messageType, out Utf8JsonWriter writer)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        buffer.Write(PrefixBytes);
        // Default writer options use the same JavaScriptEncoder as JsonSerializer's defaults,
        // which is what makes the escaping identical to upstream.
        writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        writer.WriteStringValue(messageType);
        return buffer;
    }

    private static string End(ArrayBufferWriter<byte> buffer, Utf8JsonWriter writer)
    {
        writer.WriteEndArray();
        writer.Flush();
        writer.Dispose();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
