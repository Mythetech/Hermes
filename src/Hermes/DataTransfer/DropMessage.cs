// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.DataTransfer;

/// <summary>
/// A message from the drop script, intercepted by window backends before app handlers see it.
/// </summary>
internal sealed record DropMessage(
    HermesDropReportKind Kind,
    long Sequence,
    double X,
    double Y,
    string? Text,
    string? Html,
    string? UriList)
{
    private const string Prefix = "{\"type\":\"hermes-drop";

    /// <summary>True for every drop-script message, so backends keep even unknown ones from app handlers.</summary>
    public static bool IsDropProtocol(string? message) =>
        message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    public static DropMessage? TryParse(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String)
                return null;

            return type.GetString() switch
            {
                "hermes-drop-reset" => new DropMessage(HermesDropReportKind.Reset, 0, 0, 0, null, null, null),
                "hermes-drop" when root.TryGetProperty("seq", out var seq)
                                   && seq.ValueKind == JsonValueKind.Number
                                   && seq.TryGetInt64(out var sequence) =>
                    new DropMessage(HermesDropReportKind.Drop, sequence, GetNumber(root, "x"), GetNumber(root, "y"),
                        GetString(root, "text"), GetString(root, "html"), GetString(root, "uriList")),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Builds a package from the DOM strings this message carries, plus file paths read elsewhere.</summary>
    public HermesDataPackage ToPackage(IReadOnlyList<string> files) => new()
    {
        Text = string.IsNullOrEmpty(Text) ? null : Text,
        Html = string.IsNullOrEmpty(Html) ? null : Html,
        Url = string.IsNullOrEmpty(UriList) ? null : FileUriList.FirstUrl(UriList),
        Files = files.Count == 0 ? null : files,
    };

    public HermesDropReport ToReport(HermesDataPackage? data) => new()
    {
        Kind = Kind,
        Sequence = Sequence,
        X = X,
        Y = Y,
        Data = data,
    };

    private static double GetNumber(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && double.IsFinite(number)
            ? number
            : 0;

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
