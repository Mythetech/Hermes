// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Forked by Mythetech from dotnet/aspnetcore tag v10.0.11, src/Components/WebView/WebView/src/IpcSender.cs.
// Modifications Copyright (c) Mythetech, licensed under the MIT License. See THIRD-PARTY-NOTICES.md.

#nullable enable

using Hermes.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.JSInterop.Infrastructure;

namespace Hermes.Blazor.WebView;

// Handles communication between the component abstractions (Renderer, NavigationManager,
// JSInterop, etc.) and the underlying transport channel.
internal sealed class IpcSender
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _messageDispatcher;
    private readonly Action<Exception> _unhandledException;

    public IpcSender(Dispatcher dispatcher, Action<string> messageDispatcher, Action<Exception> unhandledException)
    {
        _dispatcher = dispatcher;
        _messageDispatcher = messageDispatcher;
        _unhandledException = unhandledException;
    }

    public void ApplyRenderBatch(long batchId, RenderBatch renderBatch)
    {
        var arrayBuilder = new ArrayBuilder<byte>(2048);
        using var memoryStream = new ArrayBuilderMemoryStream(arrayBuilder);
        using (var renderBatchWriter = new RenderBatchWriter(memoryStream, false))
        {
            renderBatchWriter.Write(in renderBatch);
        }

        Dispatch(IpcMessageWriter.RenderBatch(batchId, arrayBuilder.Buffer.AsSpan(0, arrayBuilder.Count)));
    }

    public void Navigate(string uri, NavigationOptions options)
        => Dispatch(IpcMessageWriter.Navigate(uri, options));

    public void Refresh(bool forceReload)
        => Dispatch(IpcMessageWriter.Refresh(forceReload));

    public void AttachToDocument(int componentId, string selector)
        => Dispatch(IpcMessageWriter.AttachToDocument(componentId, selector));

    public void BeginInvokeJS(in JSInvocationInfo invocationInfo)
        => Dispatch(IpcMessageWriter.BeginInvokeJS(invocationInfo));

    public void EndInvokeDotNet(string callId, bool success, string? invocationResultOrError)
        => Dispatch(IpcMessageWriter.EndInvokeDotNet(callId, success, invocationResultOrError));

    public void SendByteArray(int id, byte[] data)
        => Dispatch(IpcMessageWriter.SendByteArrayToJS(id, data));

    public void SetHasLocationChangingListeners(bool hasListeners)
        => Dispatch(IpcMessageWriter.SetHasLocationChangingListeners(hasListeners));

    public void EndLocationChanging(int callId, bool shouldContinueNavigation)
        => Dispatch(IpcMessageWriter.EndLocationChanging(callId, shouldContinueNavigation));

    public void NotifyUnhandledException(Exception exception)
    {
        // The page hears first so blazor.webview.js can show its error UI, then the host.
        // Upstream instead rethrew through the dispatcher into a task nothing awaited, which
        // is why every stage 0 failure was invisible. A failure to deliver this particular
        // message is only logged: reporting it through this method again would recurse.
        var message = IpcMessageWriter.NotifyUnhandledException(exception.Message, exception.StackTrace);
        Send(message, sendFailure => HermesLogger.Warning($"Could not report an unhandled exception to the WebView: {sendFailure.Message}"));
        _unhandledException(exception);
    }

    private void Dispatch(string message) => Send(message, NotifyUnhandledException);

    private void Send(string message, Action<Exception> onFailure)
    {
        Task task;
        try
        {
            task = _dispatcher.InvokeAsync(() => _messageDispatcher(message));
        }
        catch (Exception ex)
        {
            // HermesDispatcher runs the work item inline on the UI thread, so the send can
            // throw synchronously rather than fault the task.
            onFailure(ex);
            return;
        }

        if (task.IsCompletedSuccessfully)
        {
            return;
        }

        // The task is only left running when the send was posted to another thread. The
        // continuation is self-contained and reports through onFailure, so discarding the
        // wrapper loses nothing.
        _ = ObserveAsync(task, onFailure);
    }

    private static async Task ObserveAsync(Task task, Action<Exception> onFailure)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            onFailure(ex);
        }
    }
}
