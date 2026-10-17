// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.ExceptionServices;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.DataTransfer;

[Collection(ClipboardCollection.Name)]
public sealed class ClipboardBackendTests : IDisposable
{
    private readonly RecordingClipboardBackend _backend = new();

    public ClipboardBackendTests() => Clipboard.SetBackendForTesting(_backend);

    public void Dispose()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        Clipboard.SetBackendForTesting(null);
    }

    [Fact]
    public void Write_ValidPackage_ReachesTheBackend()
    {
        var package = new HermesDataPackage { Text = "a", Html = "<b>a</b>" };

        Clipboard.Write(package);

        Assert.Same(package, _backend.Content);
    }

    [Fact]
    public void Write_InvalidPackage_ThrowsBeforeTheBackend()
    {
        Assert.Throws<ArgumentException>(() => Clipboard.Write(new HermesDataPackage()));

        Assert.Empty(_backend.Operations);
    }

    [Fact]
    public void SetText_WritesATextOnlyPackage()
    {
        Clipboard.SetText("hello");

        Assert.Equal("hello", _backend.Content.Text);
        Assert.Equal(HermesDataFormats.Text, _backend.Content.Formats);
    }

    [Fact]
    public void GetText_ReturnsNullForEmptyText()
    {
        _backend.Content = new HermesDataPackage { Text = "" };

        Assert.Null(Clipboard.GetText());
    }

    [Fact]
    public void Read_PassesFormatsAndCustomTypes()
    {
        _backend.Content = new HermesDataPackage
        {
            Text = "t",
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/x-a"] = new byte[] { 7 } },
        };

        var result = Clipboard.Read(HermesDataFormats.Text, ["application/x-a"]);

        Assert.Equal("t", result.Text);
        Assert.Equal(new byte[] { 7 }, result.Custom["application/x-a"].ToArray());
    }

    [Fact]
    public void Read_InvalidCustomType_Throws() =>
        Assert.Throws<ArgumentException>(() => Clipboard.Read(HermesDataFormats.None, ["text/plain"]));

    [Fact]
    public void UnsupportedBackend_ThrowsPlatformNotSupported()
    {
        Clipboard.SetBackendForTesting(new UnsupportedClipboardBackend("no clipboard here"));

        Assert.Throws<PlatformNotSupportedException>(() => Clipboard.GetText());
        Assert.Throws<PlatformNotSupportedException>(() => Clipboard.Write(new HermesDataPackage { Text = "a" }));
        Assert.Throws<PlatformNotSupportedException>(() => Clipboard.Changed += () => { });
    }

    [Fact]
    public void Changed_StartsMonitoringOnFirstSubscriber_AndStopsOnLast()
    {
        Action first = () => { };
        Action second = () => { };

        Clipboard.Changed += first;
        Clipboard.Changed += second;
        Assert.True(_backend.IsMonitoring);

        Clipboard.Changed -= first;
        Assert.True(_backend.IsMonitoring);

        Clipboard.Changed -= second;
        Assert.False(_backend.IsMonitoring);
        Assert.Equal(new[] { "StartMonitoring", "StopMonitoring" }, _backend.Operations);
    }

    [Fact]
    public void Changed_SameHandlerTwice_KeepsMonitoringUntilTheLastRemove()
    {
        // xUnit v2 runs sync tests under its own context, which Changed would capture and post to.
        SynchronizationContext.SetSynchronizationContext(null);
        var calls = 0;
        Action handler = () => calls++;

        Clipboard.Changed += handler;
        Clipboard.Changed += handler;
        Clipboard.Changed -= handler;
        _backend.RaiseChanged();

        Assert.True(_backend.IsMonitoring);
        Assert.Equal(1, calls);

        Clipboard.Changed -= handler;
        Assert.False(_backend.IsMonitoring);
    }

    [Fact]
    public void Changed_ThrowingHandler_DoesNotStopOthers_AndIsRouted()
    {
        // xUnit v2 runs sync tests under its own context, which Changed would capture and post to.
        SynchronizationContext.SetSynchronizationContext(null);
        Exception? routed = null;
        Action<Exception> sink = e => routed = e;
        HermesApplication.DispatcherUnhandledException += sink;
        var calls = 0;
        Action throwing = () => throw new InvalidOperationException("boom");
        Action counting = () => calls++;

        try
        {
            Clipboard.Changed += throwing;
            Clipboard.Changed += counting;
            _backend.RaiseChanged();
        }
        finally
        {
            Clipboard.Changed -= throwing;
            Clipboard.Changed -= counting;
            HermesApplication.DispatcherUnhandledException -= sink;
        }

        Assert.Equal(1, calls);
        Assert.IsType<InvalidOperationException>(routed);
    }

    [Fact]
    public void Changed_PostsToTheSubscribersSynchronizationContext()
    {
        var context = new RecordingSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);
        var calls = 0;
        Action handler = () => calls++;
        Clipboard.Changed += handler;
        SynchronizationContext.SetSynchronizationContext(null);

        _backend.RaiseChanged();
        Clipboard.Changed -= handler;

        Assert.Equal(1, context.PostCount);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Changed_ContextPostThrows_StillRunsOtherHandlers_AndIsRouted()
    {
        Exception? routed = null;
        Action<Exception> sink = e => routed = e;
        HermesApplication.DispatcherUnhandledException += sink;
        var calls = 0;
        Action posted = () => { };
        Action direct = () => calls++;

        try
        {
            SynchronizationContext.SetSynchronizationContext(new ThrowingPostSynchronizationContext());
            Clipboard.Changed += posted;
            // xUnit v2 runs sync tests under its own context, which Changed would capture and post to.
            SynchronizationContext.SetSynchronizationContext(null);
            Clipboard.Changed += direct;
            _backend.RaiseChanged();
        }
        finally
        {
            Clipboard.Changed -= posted;
            Clipboard.Changed -= direct;
            HermesApplication.DispatcherUnhandledException -= sink;
        }

        Assert.Equal(1, calls);
        Assert.IsType<InvalidOperationException>(routed);
    }

    [Fact]
    public void Changed_StopMonitoringThrows_IsRoutedAndLeavesNoTrace()
    {
        // xUnit v2 runs sync tests under its own context, which Changed would capture and post to.
        SynchronizationContext.SetSynchronizationContext(null);
        Exception? routed = null;
        Action<Exception> sink = e => routed = e;
        HermesApplication.DispatcherUnhandledException += sink;
        var failure = new InvalidOperationException("stop failed");
        var oldCalls = 0;
        var newCalls = 0;
        Action old = () => oldCalls++;
        Action replacement = () => newCalls++;

        try
        {
            Clipboard.Changed += old;
            _backend.StopMonitoringException = failure;
            Clipboard.Changed -= old;
            _backend.StopMonitoringException = null;
            Clipboard.Changed += replacement;
            _backend.RaiseChanged();
        }
        finally
        {
            Clipboard.Changed -= replacement;
            HermesApplication.DispatcherUnhandledException -= sink;
        }

        Assert.Same(failure, routed);
        Assert.Equal(0, oldCalls);
        Assert.Equal(1, newCalls);
        Assert.Equal(new[] { "StartMonitoring", "StopMonitoring", "StartMonitoring", "StopMonitoring" }, _backend.Operations);
    }

    [Fact]
    public void HasCustom_PlatformNativeName_IsAllowed()
    {
        _backend.Content = new HermesDataPackage
        {
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["image/tiff"] = new byte[] { 1 } },
        };

        Assert.True(Clipboard.HasCustom("image/tiff"));
    }

    [Fact]
    public void Changed_StartMonitoringThrows_LeavesNoTraceAndNextSubscriberRetries()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var failure = new InvalidOperationException("start failed");
        _backend.StartMonitoringException = failure;
        var failedCalls = 0;
        var retryCalls = 0;
        Action failed = () => failedCalls++;
        Action retry = () => retryCalls++;

        var thrown = Assert.Throws<InvalidOperationException>(() => Clipboard.Changed += failed);
        _backend.StartMonitoringException = null;
        Clipboard.Changed += retry;
        _backend.RaiseChanged();

        Assert.Same(failure, thrown);
        Assert.True(_backend.IsMonitoring);
        Assert.Equal(0, failedCalls);
        Assert.Equal(1, retryCalls);
        Assert.Equal(new[] { "StartMonitoring", "StartMonitoring" }, _backend.Operations);
    }

    [Fact]
    public async Task Changed_RaisedFromAnotherThreadDuringStartMonitoring_DoesNotDeadlock()
    {
        var backend = new ThreadWaitingBackend();
        backend.WorkDuringStart = backend.RaiseChanged;
        Clipboard.SetBackendForTesting(backend);

        var subscribe = Task.Run(() => Clipboard.Changed += () => { });

        Assert.Same(subscribe, await Task.WhenAny(subscribe, Task.Delay(TimeSpan.FromSeconds(5))));
        await subscribe;
    }

    [Fact]
    public async Task Clipboard_ReadDuringStartMonitoringOnAnotherThread_DoesNotDeadlock()
    {
        var backend = new ThreadWaitingBackend { WorkDuringStart = () => Clipboard.GetFormats() };
        Clipboard.SetBackendForTesting(backend);

        var subscribe = Task.Run(() => Clipboard.Changed += () => { });

        Assert.Same(subscribe, await Task.WhenAny(subscribe, Task.Delay(TimeSpan.FromSeconds(5))));
        await subscribe;
    }

    /// <summary>Waits on another thread while starting, as a backend that dispatches to the UI thread would.</summary>
    private sealed class ThreadWaitingBackend : IClipboardBackend
    {
        // Bounded so a regression fails the test instead of hanging the run.
        private static readonly TimeSpan s_joinTimeout = TimeSpan.FromSeconds(10);

        public Action? WorkDuringStart { get; set; }

        public bool IsSupported => true;

        public string? UnsupportedReason => null;

        public event Action? Changed;

        public void RaiseChanged() => Changed?.Invoke();

        public HermesDataFormats GetFormats() => HermesDataFormats.None;

        public bool HasCustom(string mimeType) => false;

        public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes) => new();

        public void Write(HermesDataPackage package)
        {
        }

        public void Clear()
        {
        }

        public void StartMonitoring()
        {
            Exception? failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    WorkDuringStart?.Invoke();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            })
            {
                IsBackground = true,
            };
            worker.Start();
            worker.Join(s_joinTimeout);

            if (failure is not null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        public void StopMonitoring()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingPostSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("context is torn down");
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostCount++;
            d(state);
        }
    }
}
