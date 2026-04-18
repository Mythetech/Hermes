// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Web.Interop;

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
    public JsonElement? Data { get; set; }
}

[JsonSerializable(typeof(InteropEnvelope))]
[JsonSerializable(typeof(ResultEnvelope))]
[JsonSerializable(typeof(ErrorEnvelope))]
[JsonSerializable(typeof(EventEnvelope))]
internal partial class InteropJsonContext : JsonSerializerContext
{
}

internal sealed class ResultEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; } = "result";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

internal sealed class ErrorEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; } = "error";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

internal sealed class EventEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; } = "event";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
