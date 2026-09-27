// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Tests.Smoke;

/// <summary>
/// A SynchronizationContext whose Post only enqueues callbacks instead of running them. Installing it
/// before starting an async call makes that call's await continuations land in this queue instead of
/// running inline or hopping to the thread pool, so a test can control exactly when they run relative
/// to other work happening concurrently on a different thread.
/// </summary>
internal sealed class QueueingSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

    public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// <summary>
    /// Runs everything currently queued, including callbacks that get queued while pumping, until none
    /// remain.
    /// </summary>
    public void PumpAll()
    {
        while (_queue.Count > 0)
        {
            var (callback, state) = _queue.Dequeue();
            callback(state);
        }
    }
}
