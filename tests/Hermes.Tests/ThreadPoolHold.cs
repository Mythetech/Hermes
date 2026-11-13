// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests;

/// <summary>Occupies every thread-pool thread, with work left queued, until disposed. Use only in a non-parallel collection.</summary>
internal sealed class ThreadPoolHold : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private readonly ManualResetEventSlim _release = new();

    public ThreadPoolHold()
    {
        // The pool can grow while this runs, so keep queueing until work stays queued behind every thread.
        var deadline = DateTime.UtcNow + Wait;
        while (ThreadPool.PendingWorkItemCount < 16)
        {
            Assert.True(DateTime.UtcNow < deadline, "The thread pool was not held.");
            for (var i = 0; i < 8; i++)
                ThreadPool.UnsafeQueueUserWorkItem(static release => release.Wait(), _release, preferLocal: false);
            Thread.Sleep(5);
        }
    }

    public void Dispose() => _release.Set();
}
