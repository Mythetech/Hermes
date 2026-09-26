// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Recognizes the message Blazor WebView sends the page when a component, navigation or interop call
/// throws. The format is Blazor's internal IPC: "__bwv:" followed by a JSON array whose first element
/// is the message type.
/// </summary>
internal static class SmokeIpcInspector
{
    private const string IpcPrefix = "__bwv:";
    private const string UnhandledExceptionPrefix = IpcPrefix + "[\"NotifyUnhandledException\"";

    internal static bool TryReadUnhandledException(string message, out string errorMessage, out string? stackTrace)
    {
        errorMessage = string.Empty;
        stackTrace = null;

        if (!message.StartsWith(UnhandledExceptionPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            using var document = JsonDocument.Parse(message.AsMemory(IpcPrefix.Length));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 2)
                return false;

            errorMessage = root[1].GetString() ?? string.Empty;
            stackTrace = root.GetArrayLength() > 2 ? root[2].GetString() : null;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
