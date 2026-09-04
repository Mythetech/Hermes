// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Hermes.Blazor.WebView.Events;
using Microsoft.JSInterop;

namespace Hermes.Blazor.WebView;

/// <summary>
/// The object blazor.webview.js calls back into for a renderer. Handed to JavaScript as a
/// <see cref="DotNetObjectReference{TValue}"/> by the renderer interop attach; never reachable
/// from .NET code. Hermes has no API for JavaScript-registered root components, so unlike
/// upstream's WebRendererInteropMethods there is only event dispatch here.
/// </summary>
internal sealed class HermesRendererInteropMethods
{
    private readonly HermesWebRenderer _renderer;
    private readonly JsonSerializerOptions _jsonOptions;

    [DynamicDependency(nameof(DispatchEventAsync))]
    public HermesRendererInteropMethods(HermesWebRenderer renderer, JsonSerializerOptions jsonOptions)
    {
        _renderer = renderer;
        _jsonOptions = jsonOptions;
    }

    [JSInvokable]
    public Task DispatchEventAsync(JsonElement eventDescriptor, JsonElement eventArgs)
    {
        var webEventData = WebEventData.Parse(_renderer, _jsonOptions, eventDescriptor, eventArgs);
        return _renderer.DispatchEventAsync(
            webEventData.EventHandlerId,
            webEventData.EventFieldInfo,
            webEventData.EventArgs);
    }
}
