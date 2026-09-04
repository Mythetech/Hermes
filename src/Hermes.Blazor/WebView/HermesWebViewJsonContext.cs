// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop.Infrastructure;

namespace Hermes.Blazor.WebView;

/// <summary>
/// Compile-time contracts for the types the framework itself moves through JS interop on
/// the way to first render and back: the JsonElement arguments of event dispatch, byte arrays,
/// the void result, and the boxed primitives that JSRuntime.InvokeAsync serializes from
/// object[]. The renderer interop object reference is deliberately absent: the generator would
/// follow it into the renderer's fields and emit contracts for the whole host layer, so
/// <see cref="DotNetObjectReferenceTypeInfoResolver"/> resolves it instead. Application types
/// are the stage 2 generator's job. This context sits ahead of the reflection resolver in
/// WebViewJSRuntime's chain, so these shapes never touch reflection even when an app leaves it
/// enabled.
/// </summary>
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(IJSVoidResult))]
internal sealed partial class HermesWebViewJsonContext : JsonSerializerContext
{
}
