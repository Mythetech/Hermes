using Microsoft.AspNetCore.Components;

namespace Hermes.Blazor.Threading;

/// <summary>
/// Blazor dispatcher that wraps HermesSynchronizationContext.
/// </summary>
internal sealed class HermesDispatcher : Dispatcher
{
    private readonly HermesSynchronizationContext _syncContext;

    public HermesDispatcher(HermesSynchronizationContext syncContext)
    {
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
    }

    public override bool CheckAccess() => _syncContext.CheckAccess();

    public override Task InvokeAsync(Action workItem)
    {
        if (CheckAccess())
        {
            workItem();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        _syncContext.Post(_ =>
        {
            try
            {
                workItem();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (CheckAccess())
        {
            return workItem();
        }

        var tcs = new TaskCompletionSource();
        _syncContext.Post(async _ =>
        {
            try
            {
                await workItem();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (CheckAccess())
        {
            return Task.FromResult(workItem());
        }

        var tcs = new TaskCompletionSource<TResult>();
        _syncContext.Post(_ =>
        {
            try
            {
                var result = workItem();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (CheckAccess())
        {
            return workItem();
        }

        var tcs = new TaskCompletionSource<TResult>();
        _syncContext.Post(async _ =>
        {
            try
            {
                var result = await workItem();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }
}
