// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using Hermes.Blazor.Diagnostics;
using Hermes.Blazor.Threading;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Smoke;

/// <summary>
/// Covers SmokeRoot.DeferPastCurrentRenderBatchAsync against a real HermesSynchronizationContext,
/// which bUnit does not use. The recording backend, like the Windows and Linux backends, runs a
/// BeginInvoke made on the UI thread inline whatever the context's busy state, so only a post from a
/// non-UI thread is guaranteed to queue. The deferred continuation must therefore wait in the
/// backend's queue until the test pumps it, after the caller's remaining work.
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
