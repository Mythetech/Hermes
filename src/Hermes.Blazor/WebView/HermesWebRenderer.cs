// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hermes.Blazor.WebView.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace Hermes.Blazor.WebView;

/// <summary>
/// The renderer behind a Hermes WebView. It derives from the public <see cref="Renderer"/>
/// rather than upstream's WebRenderer so that the renderer interop attach, the render batch
/// acknowledgment queue and event dispatch are Hermes code with explicit, testable contracts.
/// Stage 0 traced the blank window under Native AOT to WebRenderer discarding the attach
/// call's faulted task; nothing here discards a task.
/// </summary>
internal sealed class HermesWebRenderer : Renderer
{
    // blazor.webview.js keys its renderer on this id (WebRendererId.WebView upstream).
    private const int WebViewRendererId = 3;

    private const string AttachInteropIdentifier = "Blazor._internal.attachWebRendererInterop";

    private static readonly RendererInfo WebViewRendererInfo = new("WebView", isInteractive: true);

    private readonly Queue<UnacknowledgedRenderBatch> _unacknowledgedRenderBatches = new();
    private readonly Dispatcher _dispatcher;
    private readonly IpcSender _ipcSender;
    private readonly DotNetObjectReference<HermesRendererInteropMethods> _interopMethodsReference;
    private long _nextRenderBatchId = 1;

    public HermesWebRenderer(
        IServiceProvider serviceProvider,
        Dispatcher dispatcher,
        IpcSender ipcSender,
        ILoggerFactory loggerFactory,
        WebViewJSRuntime jsRuntime)
        : base(serviceProvider, loggerFactory)
    {
        _dispatcher = dispatcher;
        _ipcSender = ipcSender;
        ElementReferenceContext = jsRuntime.ElementReferenceContext;

        var jsonOptions = jsRuntime.ReadJsonSerializerOptions();
        _interopMethodsReference = DotNetObjectReference.Create(new HermesRendererInteropMethods(this, jsonOptions));
        AttachWebRendererInterop(jsonOptions);
    }

    public override Dispatcher Dispatcher => _dispatcher;

    protected override RendererInfo RendererInfo => WebViewRendererInfo;

    public int AddRootComponent([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType, string domElementSelector)
    {
        var component = InstantiateComponent(componentType);
        var componentId = AssignRootComponentId(component);
        _ipcSender.AttachToDocument(componentId, domElementSelector);
        return componentId;
    }

    public new Task RenderRootComponentAsync(int componentId, ParameterView parameters)
        => base.RenderRootComponentAsync(componentId, parameters);

    public new void RemoveRootComponent(int componentId)
        => base.RemoveRootComponent(componentId);

    public void NotifyRenderCompleted(long batchId)
    {
        if (!_unacknowledgedRenderBatches.TryDequeue(out var nextUnacknowledgedBatch))
        {
            throw new InvalidOperationException($"Received an acknowledgement for render batch {batchId} but no batch is awaiting one.");
        }

        if (nextUnacknowledgedBatch.BatchId != batchId)
        {
            throw new InvalidOperationException($"Received unexpected acknowledgement for render batch {batchId} (next batch should be {nextUnacknowledgedBatch.BatchId})");
        }

        nextUnacknowledgedBatch.CompletionSource.SetResult();
    }

    protected override void HandleException(Exception exception)
        => _ipcSender.NotifyUnhandledException(exception);

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        var batchId = _nextRenderBatchId++;
        var tcs = new TaskCompletionSource();
        _unacknowledgedRenderBatches.Enqueue(new UnacknowledgedRenderBatch(batchId, tcs));
        _ipcSender.ApplyRenderBatch(batchId, renderBatch);
        return tcs.Task;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _interopMethodsReference.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AttachWebRendererInterop(JsonSerializerOptions jsonOptions)
    {
        // The payload is [rendererId, interopMethods, jsComponentParameters, jsComponentInitializers].
        // Hermes has no API for JavaScript-registered root components, so the last two are
        // always empty objects. Only the object reference needs the serializer: its converter
        // is what assigns the id JavaScript will call back with.
        var referenceTypeInfo = (JsonTypeInfo<DotNetObjectReference<HermesRendererInteropMethods>>)
            jsonOptions.GetTypeInfo(typeof(DotNetObjectReference<HermesRendererInteropMethods>));
        var referenceJson = JsonSerializer.Serialize(_interopMethodsReference, referenceTypeInfo);

        // AsyncHandle 0 tells blazor.webview.js that no completion is wanted. Upstream allocated
        // a handle and then discarded the completion task, which is the swallow that hid the
        // stage 0 failure; nothing on either side needs the reply.
        _ipcSender.BeginInvokeJS(new JSInvocationInfo
        {
            AsyncHandle = 0,
            TargetInstanceId = 0,
            Identifier = AttachInteropIdentifier,
            CallType = JSCallType.FunctionCall,
            ResultType = JSCallResultType.JSVoidResult,
            ArgsJson = $"[{WebViewRendererId},{referenceJson},{{}},{{}}]",
        });
    }

    private readonly record struct UnacknowledgedRenderBatch(long BatchId, TaskCompletionSource CompletionSource);
}
