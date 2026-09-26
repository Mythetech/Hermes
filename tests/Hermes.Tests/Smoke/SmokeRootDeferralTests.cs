// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using Hermes.Blazor.Diagnostics;
using Hermes.Blazor.Threading;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Smoke;

/// <summary>
/// Covers SmokeRoot.DeferPastCurrentRenderBatchAsync under real Hermes synchronization semantics
/// (not bUnit, which does not reproduce HermesSynchronizationContext.Post's inline-vs-queued behavior).
/// A plain Task.Yield() schedules its continuation by calling SynchronizationContext.Post
/// synchronously, on the same thread that is awaiting it; HermesSynchronizationContext.Post runs
/// inline whenever that call happens on the UI thread while the context is not already marked busy,
/// which is exactly the state right after the render loop calls OnAfterRenderAsync directly. A bare
/// Task.Run(() => {}) does not fix this either: its trivial work item frequently finishes on the
/// thread pool before the awaiting method's compiler-generated check ever looks at it, so the await
/// short-circuits synchronously without posting through the SynchronizationContext at all, and the
/// continuation still runs inline on the UI thread (confirmed empirically here: with a bare Task.Run
/// this test fails on essentially every run). A fixed-length Task.Delay is not safe either: it only
/// moves the same race to a wall-clock margin, and a loaded thread pool or CI runner can stall the
/// checking thread past that margin (this was caught by running the whole suite: with a
/// Task.Delay(1)-based deferral this specific test passed every time in isolation but failed when run
/// alongside the other 549+ tests). DeferredAwaitable.IsCompleted is hard-coded false, so the compiler
/// can never take the synchronous shortcut regardless of load, which removes the race entirely rather
/// than just narrowing it.
/// </summary>
public class SmokeRootDeferralTests
{
    [Fact]
    public void DeferPastCurrentRenderBatchAsync_QueuesThroughTheBackend_NotInline()
    {
        var backend = new RecordingWindowBackend();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new HermesSynchronizationContext(backend));
        try
        {
            var order = new List<string>();
            var helperTask = RunHelperAsync(order);

            Assert.False(helperTask.IsCompleted);
            order.Add("after-render");

            var stopwatch = Stopwatch.StartNew();
            while (!helperTask.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                backend.ProcessPending();
                if (!helperTask.IsCompleted)
                    Thread.Sleep(1);
            }

            Assert.True(helperTask.IsCompleted, "The deferred continuation never ran; ProcessPending never saw it queued.");
            Assert.Equal(new[] { "after-render", "deferred" }, order);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static async Task RunHelperAsync(List<string> order)
    {
        await SmokeRoot.DeferPastCurrentRenderBatchAsync();
        order.Add("deferred");
    }
}
