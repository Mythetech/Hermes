// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hermes.Web.Interop;

/// <summary>
/// The bridge's payload serialization: its options, and the outgoing envelopes written around payloads.
/// </summary>
internal static class BridgeJson
{
    private static readonly JsonElement NullElement = JsonDocument.Parse("null").RootElement.Clone();

    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.TypeInfoResolverChain.Add(BridgeValueJsonContext.Default);
        return options;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Added only when reflection-based serialization is enabled, which trimmed and native AOT apps turn off.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Added only when reflection-based serialization is enabled, which trimmed and native AOT apps turn off.")]
    public static void Freeze(JsonSerializerOptions options, bool useReflection)
    {
        if (options.IsReadOnly)
            return;

        if (useReflection)
            options.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());

        options.MakeReadOnly();
    }

    public static JsonTypeInfo GetTypeInfo(JsonSerializerOptions options, Type type)
    {
        if (options.TryGetTypeInfo(type, out var typeInfo))
            return typeInfo;

        throw new NotSupportedException(
            $"The bridge has no JSON metadata for {type.FullName}. Add a JsonSerializerContext that includes it to Bridge.SerializerOptions.TypeInfoResolverChain.");
    }

    /// <summary>Reads a payload, treating a missing value as JSON null.</summary>
    public static object? Read(JsonElement element, JsonTypeInfo typeInfo) =>
        JsonSerializer.Deserialize(element.ValueKind == JsonValueKind.Undefined ? NullElement : element, typeInfo);

    public static string Result(string id, object? value, JsonTypeInfo? valueType) => Write(writer =>
    {
        writer.WriteString("type", "result");
        writer.WriteString("id", id);
        writer.WritePropertyName("value");
        WriteValue(writer, value, valueType);
    });

    public static string Error(string id, string message) => Write(writer =>
    {
        writer.WriteString("type", "error");
        writer.WriteString("id", id);
        writer.WriteString("message", message);
    });

    public static string Event(string name, object? data, JsonTypeInfo? dataType) => Write(writer =>
    {
        writer.WriteString("type", "event");
        writer.WriteString("name", name);
        writer.WritePropertyName("data");
        WriteValue(writer, data, dataType);
    });

    private static void WriteValue(Utf8JsonWriter writer, object? value, JsonTypeInfo? typeInfo)
    {
        if (value is null || typeInfo is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, value, typeInfo);
    }

    private static string Write(Action<Utf8JsonWriter> writeProperties)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}

/// <summary>
/// Metadata for the payload types every app gets without adding a context of its own.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(double?))]
internal sealed partial class BridgeValueJsonContext : JsonSerializerContext;
