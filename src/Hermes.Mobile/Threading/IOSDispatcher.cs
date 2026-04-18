// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using CoreFoundation;
using Foundation;
using Microsoft.AspNetCore.Components;

namespace Hermes.Mobile.Threading;

/// <summary>
/// Marshals Blazor component work onto the iOS main queue (UI thread).
/// </summary>
internal sealed class IOSDispatcher : Dispatcher
{
    public override bool CheckAccess() => NSThread.IsMain;

    public override Task InvokeAsync(Action workItem)
    {
        var tcs = new TaskCompletionSource();
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try { workItem(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        var tcs = new TaskCompletionSource();
        DispatchQueue.MainQueue.DispatchAsync(async () =>
        {
            try { await workItem().ConfigureAwait(false); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        var tcs = new TaskCompletionSource<TResult>();
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try { tcs.SetResult(workItem()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        var tcs = new TaskCompletionSource<TResult>();
        DispatchQueue.MainQueue.DispatchAsync(async () =>
        {
            try { tcs.SetResult(await workItem().ConfigureAwait(false)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }
}
