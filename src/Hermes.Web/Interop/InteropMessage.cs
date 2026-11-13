// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Web.Interop;

/// <summary>
/// An incoming bridge message: an invoke, an event, the page's ready signal, or an error in the page.
/// </summary>
internal sealed class InteropEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("args")]
    public JsonElement[]? Args { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("stack")]
    public string? Stack { get; set; }
}

/// <summary>
/// An uncaught error, an unhandled rejection, or a throwing event callback in the page.
/// </summary>
internal sealed record PageError(string Source, string Name, string Message, string? Stack);

[JsonSerializable(typeof(InteropEnvelope))]
internal partial class InteropJsonContext : JsonSerializerContext
{
}
