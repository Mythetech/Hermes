// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Testing;
using Hermes.Tests.Smoke;
using Hermes.Web.Interop;
using Xunit;

namespace Hermes.Tests.Web;

/// <summary>Holds the whole thread pool, so it runs alone.</summary>
[Collection(nameof(ProcessWideEventsCollection))]
public sealed class InteropBridgeStopTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Stop cancels handlers on the thread pool; when the pool is busy, Stop can return before that runs.
    /// </summary>
    [Fact]
    public async Task Stop_DropsQueuedEvents_BeforeTheCancellationRuns()
    {
        var gate = new TaskCompletionSource();
        var entered = new ManualResetEventSlim();
        var later = 0;
        var backend = new RecordingWindowBackend();
        var bridge = new InteropBridge(backend) { StopTimeout = TimeSpan.FromMilliseconds(50) };
        bridge.On("slow", async () =>
        {
            entered.Set();
            await gate.Task;
        });
        bridge.On("later", () => Interlocked.Increment(ref later));
        bridge.Handle("ping", () => "pong");
        bridge.Start();

        backend.SimulateWebMessage("""{"type":"event","name":"slow","data":null}""");
        backend.SimulateWebMessage("""{"type":"event","name":"later","data":null}""");
        backend.SimulateWebMessage("""{"type":"invoke","id":"1","method":"ping","args":[]}""");
        Assert.True(entered.Wait(Wait));
        await RepliedAsync(backend);

        try
        {
            using (new ThreadPoolHold())
            {
                // A plain thread, like the UI thread: no synchronization context, so the handler resumes inline.
                var stopper = new Thread(() =>
                {
                    bridge.Stop();
                    gate.SetResult();
                });
                stopper.Start();
                Assert.True(stopper.Join(Wait));
            }

            Assert.Equal(0, Volatile.Read(ref later));
        }
        finally
        {
            gate.TrySetResult();
            bridge.Stop();
        }
    }

    /// <summary>The reply to a call sent after both events shows the pump has queued them.</summary>
    private static async Task RepliedAsync(RecordingWindowBackend backend)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (backend.Recording.WebMessagesSent.Count == 0 && DateTime.UtcNow < deadline)
        {
            backend.ProcessPending();
            await Task.Delay(5);
        }

        Assert.NotEmpty(backend.Recording.WebMessagesSent);
    }

    /// <summary>Occupies every thread-pool thread, with work left queued, until disposed.</summary>
    private sealed class ThreadPoolHold : IDisposable
    {
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
}
