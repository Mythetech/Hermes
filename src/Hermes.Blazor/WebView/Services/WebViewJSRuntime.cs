// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Forked by Mythetech from dotnet/aspnetcore tag v10.0.11, src/Components/WebView/WebView/src/Services/WebViewJSRuntime.cs.
// Modifications Copyright (c) Mythetech, licensed under the MIT License. See THIRD-PARTY-NOTICES.md.

#nullable disable warnings

using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace Hermes.Blazor.WebView.Services;

internal sealed class WebViewJSRuntime : JSRuntime
{
    private IpcSender _ipcSender;

    public ElementReferenceContext ElementReferenceContext { get; }

    public WebViewJSRuntime()
    {
        ElementReferenceContext = new WebElementReferenceContext(this);
        JsonSerializerOptions.Converters.Add(
            new ElementReferenceJsonConverter(
                new WebElementReferenceContext(this)));

        // Framework interop shapes resolve from the generated context, so Native AOT never
        // asks the reflection resolver for them. Application types still fall back to
        // reflection while the app has it enabled (every JIT build today), which keeps existing
        // apps working unchanged until the stage 2 generator supplies their contexts.
        JsonSerializerOptions.TypeInfoResolverChain.Add(new DotNetObjectReferenceTypeInfoResolver());
        JsonSerializerOptions.TypeInfoResolverChain.Add(HermesWebViewJsonContext.Default);
        AddReflectionFallback(JsonSerializerOptions);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Guarded by the JsonSerializer.IsReflectionEnabledByDefault feature switch; trimmed and AOT builds with reflection disabled never reach the resolver construction.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Guarded by the JsonSerializer.IsReflectionEnabledByDefault feature switch; trimmed and AOT builds with reflection disabled never reach the resolver construction.")]
    private static void AddReflectionFallback(JsonSerializerOptions options)
    {
        if (JsonSerializer.IsReflectionEnabledByDefault)
        {
            options.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());
        }
    }

    public void AttachToWebView(IpcSender ipcSender)
    {
        _ipcSender = ipcSender;
    }

    public JsonSerializerOptions ReadJsonSerializerOptions() => JsonSerializerOptions;

    protected override void BeginInvokeJS(long taskId, string identifier, string argsJson, JSCallResultType resultType, long targetInstanceId)
    {
        var invocationInfo = new JSInvocationInfo
        {
            AsyncHandle = taskId,
            Identifier = identifier,
            ArgsJson = argsJson,
            CallType = JSCallType.FunctionCall,
            ResultType = resultType,
            TargetInstanceId = targetInstanceId,
        };

        BeginInvokeJS(invocationInfo);
    }

    protected override void BeginInvokeJS(in JSInvocationInfo invocationInfo)
    {
        if (_ipcSender is null)
        {
            throw new InvalidOperationException("Cannot invoke JavaScript outside of a WebView context.");
        }

        _ipcSender.BeginInvokeJS(invocationInfo);
    }

    protected override void EndInvokeDotNet(DotNetInvocationInfo invocationInfo, in DotNetInvocationResult invocationResult)
    {
        var resultJsonOrErrorMessage = invocationResult.Success
            ? invocationResult.ResultJson
            : invocationResult.Exception.ToString();
        _ipcSender.EndInvokeDotNet(invocationInfo.CallId, invocationResult.Success, resultJsonOrErrorMessage);
    }

    protected override void SendByteArray(int id, byte[] data)
    {
        _ipcSender.SendByteArray(id, data);
    }

    protected override Task<Stream> ReadJSDataAsStreamAsync(IJSStreamReference jsStreamReference, long totalLength, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(PullFromJSDataStream.CreateJSDataStream(this, jsStreamReference, totalLength, cancellationToken));

    protected override Task TransmitStreamAsync(long streamId, DotNetStreamReference dotNetStreamReference)
    {
        return TransmitDataStreamToJS.TransmitStreamAsync(this, "Blazor._internal.receiveWebViewDotNetDataStream", streamId, dotNetStreamReference);
    }
}
