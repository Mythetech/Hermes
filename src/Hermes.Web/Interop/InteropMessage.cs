// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Web.Interop;

/// <summary>
/// An incoming bridge message: an invoke with its arguments, or an event with its data.
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
}

[JsonSerializable(typeof(InteropEnvelope))]
internal partial class InteropJsonContext : JsonSerializerContext
{
}
