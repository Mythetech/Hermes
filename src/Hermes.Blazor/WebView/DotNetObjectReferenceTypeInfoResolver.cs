// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

namespace Hermes.Blazor.WebView;

/// <summary>
/// Resolves the renderer interop object reference to the JSRuntime's own converter, the way
/// upstream's JsonConverterFactoryTypeInfoResolver does for the WebAssembly fast path. The
/// converter is what assigns the object id JavaScript calls back with, so the generated
/// context must not describe this type as a plain object.
/// </summary>
internal sealed class DotNetObjectReferenceTypeInfoResolver : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (type != typeof(DotNetObjectReference<HermesRendererInteropMethods>))
        {
            return null;
        }

        foreach (var converter in options.Converters)
        {
            if (converter is JsonConverterFactory factory && factory.CanConvert(type))
            {
                var created = factory.CreateConverter(type, options)!;
                return JsonMetadataServices.CreateValueInfo<DotNetObjectReference<HermesRendererInteropMethods>>(options, created);
            }
        }

        return null;
    }
}
