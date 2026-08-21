// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Infrastructure;
using Xunit;

namespace Hermes.Tests;

public class UiInvokeQueueTests
{
    private static UiInvokeQueue CreateQueue(List<Exception>? unhandled = null)
    {
        return new UiInvokeQueue(ex => unhandled?.Add(ex));
    }

    private static TaskCompletionSource CreateCompletion()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [Fact]
    public void TryEnqueue_FirstItem_RequestsWakeup()
    {
        using var queue = CreateQueue();

        bool accepted = queue.TryEnqueue(() => { }, completion: null, out bool wakeupNeeded);

        Assert.True(accepted);
        Assert.True(wakeupNeeded);
    }

    [Fact]
    public void TryEnqueue_WhileWakeupPending_DoesNotRequestAnotherWakeup()
    {
        using var queue = CreateQueue();

        queue.TryEnqueue(() => { }, completion: null, out _);
        queue.TryEnqueue(() => { }, completion: null, out bool secondWakeup);
        queue.TryEnqueue(() => { }, completion: null, out bool thirdWakeup);

        Assert.False(secondWakeup);
        Assert.False(thirdWakeup);
    }

    [Fact]
    public void Drain_ExecutesAllItemsInOrder()
    {
        using var queue = CreateQueue();
        var executed = new List<int>();

        queue.TryEnqueue(() => executed.Add(1), completion: null, out _);
        queue.TryEnqueue(() => executed.Add(2), completion: null, out _);
        queue.TryEnqueue(() => executed.Add(3), completion: null, out _);

        queue.Drain();

        Assert.Equal(new[] { 1, 2, 3 }, executed);
    }

    [Fact]
    public void TryEnqueue_AfterDrain_RequestsWakeupAgain()
    {
        using var queue = CreateQueue();

        queue.TryEnqueue(() => { }, completion: null, out _);
        queue.Drain();
        queue.TryEnqueue(() => { }, completion: null, out bool wakeupNeeded);

        Assert.True(wakeupNeeded);
    }

    [Fact]
    public void Drain_CompletesSyncItemCompletion()
    {
        using var queue = CreateQueue();
        var completion = CreateCompletion();

        queue.TryEnqueue(() => { }, completion, out _);
        queue.Drain();

        Assert.True(completion.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public void Drain_SyncItemThrows_FaultsCompletionAndContinuesDraining()
    {
        using var queue = CreateQueue();
        var completion = CreateCompletion();
        var laterItemRan = false;

        queue.TryEnqueue(() => throw new InvalidOperationException("boom"), completion, out _);
        queue.TryEnqueue(() => laterItemRan = true, completion: null, out _);

        queue.Drain();

        Assert.True(completion.Task.IsFaulted);
        Assert.IsType<InvalidOperationException>(completion.Task.Exception!.GetBaseException());
        Assert.True(laterItemRan);
    }

    [Fact]
    public void Drain_AsyncItemThrows_RoutesToUnhandledExceptionHandler()
    {
        var unhandled = new List<Exception>();
        using var queue = CreateQueue(unhandled);

        queue.TryEnqueue(() => throw new InvalidOperationException("async boom"), completion: null, out _);
        queue.Drain();

        var exception = Assert.Single(unhandled);
        Assert.Equal("async boom", exception.Message);
    }

    [Fact]
    public void Drain_UnhandledExceptionHandlerThrows_DoesNotPropagate()
    {
        var queue = new UiInvokeQueue(_ => throw new InvalidOperationException("handler boom"));
        queue.TryEnqueue(() => throw new InvalidOperationException("original"), completion: null, out _);

        var drainException = Record.Exception(() => queue.Drain());

        Assert.Null(drainException);
        queue.Dispose();
    }

    [Fact]
    public void Dispose_FaultsPendingSyncCompletions()
    {
        var queue = CreateQueue();
        var completion = CreateCompletion();
        queue.TryEnqueue(() => { }, completion, out _);

        queue.Dispose();

        Assert.True(completion.Task.IsFaulted);
        Assert.IsType<ObjectDisposedException>(completion.Task.Exception!.GetBaseException());
    }

    [Fact]
    public void Dispose_DoesNotExecutePendingActions()
    {
        var queue = CreateQueue();
        var ran = false;
        queue.TryEnqueue(() => ran = true, completion: null, out _);

        queue.Dispose();

        Assert.False(ran);
    }

    [Fact]
    public void TryEnqueue_AfterDispose_IsRejectedWithoutThrowing()
    {
        var queue = CreateQueue();
        queue.Dispose();

        bool accepted = queue.TryEnqueue(() => { }, completion: null, out bool wakeupNeeded);

        Assert.False(accepted);
        Assert.False(wakeupNeeded);
    }

    [Fact]
    public void Dispose_UnblocksThreadWaitingOnCompletion()
    {
        var queue = CreateQueue();
        var completion = CreateCompletion();
        queue.TryEnqueue(() => { }, completion, out _);

        var waiter = Task.Run(() =>
        {
            try
            {
                completion.Task.GetAwaiter().GetResult();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        queue.Dispose();

        bool unblocked = waiter.Wait(TimeSpan.FromSeconds(5));
        Assert.True(unblocked);
        Assert.IsType<ObjectDisposedException>(waiter.Result);
    }
}

public class DispatcherUnhandledExceptionTests
{
    [Fact]
    public void RaiseDispatcherUnhandledException_DeliversToSubscriber()
    {
        Exception? received = null;
        Action<Exception> subscriber = ex => received = ex;
        HermesApplication.DispatcherUnhandledException += subscriber;

        try
        {
            var thrown = new InvalidOperationException("dispatcher boom");
            HermesApplication.RaiseDispatcherUnhandledException(thrown);

            Assert.Same(thrown, received);
        }
        finally
        {
            HermesApplication.DispatcherUnhandledException -= subscriber;
        }
    }

    [Fact]
    public void RaiseDispatcherUnhandledException_SubscriberThrows_DoesNotPropagate()
    {
        Action<Exception> subscriber = _ => throw new InvalidOperationException("subscriber boom");
        HermesApplication.DispatcherUnhandledException += subscriber;

        try
        {
            var raiseException = Record.Exception(() =>
                HermesApplication.RaiseDispatcherUnhandledException(new InvalidOperationException("original")));

            Assert.Null(raiseException);
        }
        finally
        {
            HermesApplication.DispatcherUnhandledException -= subscriber;
        }
    }
}
