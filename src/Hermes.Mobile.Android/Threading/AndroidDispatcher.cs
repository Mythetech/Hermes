// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.OS;
using Microsoft.AspNetCore.Components;

namespace Hermes.Mobile.Android.Threading;

internal sealed class AndroidDispatcher : Dispatcher
{
    private readonly Handler _handler = new(Looper.MainLooper!);

    public override bool CheckAccess() => Looper.MyLooper() == Looper.MainLooper;

    public override Task InvokeAsync(Action workItem)
    {
        if (CheckAccess())
        {
            try { workItem(); return Task.CompletedTask; }
            catch (Exception ex) { return Task.FromException(ex); }
        }

        var tcs = new TaskCompletionSource();
        _handler.Post(() =>
        {
            try { workItem(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (CheckAccess())
            return workItem();

        var tcs = new TaskCompletionSource();
        _handler.Post(async () =>
        {
            try { await workItem().ConfigureAwait(false); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (CheckAccess())
        {
            try { return Task.FromResult(workItem()); }
            catch (Exception ex) { return Task.FromException<TResult>(ex); }
        }

        var tcs = new TaskCompletionSource<TResult>();
        _handler.Post(() =>
        {
            try { tcs.SetResult(workItem()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (CheckAccess())
            return workItem();

        var tcs = new TaskCompletionSource<TResult>();
        _handler.Post(async () =>
        {
            try { tcs.SetResult(await workItem().ConfigureAwait(false)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }
}
