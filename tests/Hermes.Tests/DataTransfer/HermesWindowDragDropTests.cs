// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.DataTransfer;

[Collection(DragDropCollection.Name)]
public sealed class HermesWindowDragDropTests
{
    private static readonly string[] s_paths =
    [
        Path.Combine(Path.GetTempPath(), "a.txt"),
        Path.Combine(Path.GetTempPath(), "b c.txt"),
    ];

    private static TestableHermesWindow CreateShown(bool dragDrop = true)
    {
        var testable = new TestableHermesWindow();
        testable.Window.SetDragDrop(dragDrop);
        testable.Window.Show();
        return testable;
    }

    private static List<string> Replies(TestableHermesWindow testable) =>
        testable.Recording.WebMessagesSent.Where(m => m.StartsWith("{\"type\":\"hermes-drop-ready\"", StringComparison.Ordinal)).ToList();

    [Fact]
    public void Drop_RaisesDroppedWithTheData_AndRepliesWithItsId()
    {
        using var testable = CreateShown();
        HermesDrop? dropped = null;
        testable.Window.Dropped += drop => dropped = drop;
        var package = new HermesDataPackage { Files = s_paths };

        testable.Backend.SimulateDrop(package, 12.5, 40);

        Assert.NotNull(dropped);
        Assert.Same(package, dropped.Data);
        Assert.Equal(12.5, dropped.X);
        Assert.Equal(40, dropped.Y);
        Assert.Equal(new[] { $$"""{"type":"hermes-drop-ready","seq":1,"dropId":"{{dropped.Id}}"}""" }, Replies(testable));
        Assert.True(testable.Window.TryGetDrop(dropped.Id, out var resolved));
        Assert.Same(dropped, resolved);
    }

    [Fact]
    public void DropWithoutData_RepliesWithoutAnId_AndRaisesNothing()
    {
        using var testable = CreateShown();
        var raised = false;
        testable.Window.Dropped += _ => raised = true;

        testable.Backend.SimulateDrop(null, 0, 0);
        testable.Backend.SimulateDrop(new HermesDataPackage(), 0, 0);

        Assert.False(raised);
        Assert.Equal(
            new[] { """{"type":"hermes-drop-ready","seq":1}""", """{"type":"hermes-drop-ready","seq":2}""" },
            Replies(testable));
    }

    [Fact]
    public void HandlerThatThrows_DoesNotStopOthers_AndIsRouted()
    {
        using var testable = CreateShown();
        var boom = new InvalidOperationException("drop handler boom");
        var secondRan = false;
        Exception? routed = null;
        Action<Exception> subscriber = ex =>
        {
            if (ReferenceEquals(ex, boom))
                routed = ex;
        };
        testable.Window.Dropped += _ => throw boom;
        testable.Window.Dropped += _ => secondRan = true;

        HermesApplication.DispatcherUnhandledException += subscriber;
        try
        {
            testable.Backend.SimulateDrop(new HermesDataPackage { Text = "x" }, 0, 0);
        }
        finally
        {
            HermesApplication.DispatcherUnhandledException -= subscriber;
        }

        Assert.True(secondRan);
        Assert.Same(boom, routed);
        Assert.Contains(Replies(testable), m => m.Contains("\"dropId\":", StringComparison.Ordinal));
    }

    [Fact]
    public void Reset_ReleasesTheWindowsDrops()
    {
        using var testable = CreateShown();
        HermesDrop? dropped = null;
        testable.Window.Dropped += drop => dropped = drop;
        testable.Backend.SimulateDrop(new HermesDataPackage { Text = "x" }, 0, 0);

        testable.Backend.SimulateDropReset();

        Assert.False(testable.Window.TryGetDrop(dropped!.Id, out _));
    }

    [Fact]
    public void Close_ReleasesTheWindowsDrops()
    {
        using var testable = CreateShown();
        HermesDrop? dropped = null;
        testable.Window.Dropped += drop => dropped = drop;
        testable.Backend.SimulateDrop(new HermesDataPackage { Text = "x" }, 0, 0);

        testable.Window.Close();

        Assert.False(testable.Window.TryGetDrop(dropped!.Id, out _));
    }

    [Fact]
    public void TryGetDrop_DoesNotResolveAnotherWindowsDrop()
    {
        using var first = CreateShown();
        using var second = CreateShown();
        HermesDrop? dropped = null;
        first.Window.Dropped += drop => dropped = drop;

        first.Backend.SimulateDrop(new HermesDataPackage { Text = "x" }, 0, 0);

        Assert.False(second.Window.TryGetDrop(dropped!.Id, out _));
    }

    [Fact]
    public void WindowWithoutDragDrop_IgnoresDropReports()
    {
        using var testable = CreateShown(dragDrop: false);
        var raised = false;
        testable.Window.Dropped += _ => raised = true;

        testable.Backend.SimulateDrop(new HermesDataPackage { Text = "x" }, 0, 0);

        Assert.False(raised);
        Assert.Empty(Replies(testable));
    }

    [Fact]
    public async Task SimulateDropForTesting_AsksThePage_AndUsesTheQueuedData()
    {
        using var testable = CreateShown();
        var package = new HermesDataPackage { Files = s_paths };

        var pending = testable.Window.SimulateDropForTesting(package, 96, 150.5);

        Assert.Equal("""{"type":"hermes-drop-simulate","x":96,"y":150.5}""", testable.Recording.WebMessagesSent[^1]);
        Assert.False(pending.IsCompleted);

        testable.Backend.SimulateDrop(null, 96, 150.5);

        var drop = await pending;
        Assert.Same(package, drop.Data);
        Assert.True(testable.Window.TryGetDrop(drop.Id, out _));
    }

    [Fact]
    public async Task StartDragAsync_PassesThePackageToTheBackend_AndReturnsItsResult()
    {
        using var testable = CreateShown();
        testable.Backend.DragResult = HermesDragResult.Canceled;
        var package = new HermesDataPackage { Text = "drag me" };

        var result = await testable.Window.StartDragAsync(package);

        Assert.Equal(HermesDragResult.Canceled, result);
        Assert.Same(package, Assert.Single(testable.Backend.DragsStarted));
    }

    [Fact]
    public async Task StartDragAsync_RejectsAnInvalidPackage()
    {
        using var testable = CreateShown();

        await Assert.ThrowsAsync<ArgumentException>(() => testable.Window.StartDragAsync(new HermesDataPackage()));
    }

    [Fact]
    public async Task StartDragAsync_RequiresDragDrop()
    {
        using var testable = CreateShown(dragDrop: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => testable.Window.StartDragAsync(new HermesDataPackage { Text = "x" }));
    }

    [Fact]
    public async Task StartDragAsync_RequiresAShownWindow()
    {
        using var testable = new TestableHermesWindow();
        testable.Window.SetDragDrop(true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => testable.Window.StartDragAsync(new HermesDataPackage { Text = "x" }));
    }

    [Fact]
    public async Task StartDragAsync_RefusesASecondDragWhileOneIsActive()
    {
        using var testable = CreateShown();
        var completion = new TaskCompletionSource<HermesDragResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        testable.Backend.DragCompletion = completion;
        var package = new HermesDataPackage { Text = "x" };

        var first = testable.Window.StartDragAsync(package);
        await Assert.ThrowsAsync<InvalidOperationException>(() => testable.Window.StartDragAsync(package));
        completion.SetResult(HermesDragResult.Dropped);

        Assert.Equal(HermesDragResult.Dropped, await first);

        // After an await the test may resume on another thread, where the first window's backend would only queue.
        using var next = CreateShown();
        Assert.Equal(HermesDragResult.Dropped, await next.Window.StartDragAsync(package));
    }

    [Fact]
    public async Task StartDragAsync_AfterTheWindowCloses_IsCanceled()
    {
        using var testable = CreateShown();
        testable.Window.Close();

        Assert.Equal(HermesDragResult.Canceled, await testable.Window.StartDragAsync(new HermesDataPackage { Text = "x" }));
        Assert.Empty(testable.Backend.DragsStarted);
    }

    [Fact]
    public async Task StartDragAsync_StillQueuedWhenTheWindowCloses_IsCanceled_AndFreesTheNextDrag()
    {
        using var testable = CreateShown();
        var package = new HermesDataPackage { Text = "x" };

        // A dedicated thread is never the recording backend's UI thread, so BeginInvoke queues and the start stays pending.
        Task<HermesDragResult>? drag = null;
        var caller = new Thread(() => drag = testable.Window.StartDragAsync(package));
        caller.Start();
        caller.Join();
        testable.Window.Close();

        Assert.Equal(HermesDragResult.Canceled, await drag!);
        Assert.Empty(testable.Backend.DragsStarted);

        using var other = CreateShown();
        Assert.Equal(HermesDragResult.Dropped, await other.Window.StartDragAsync(package));
    }

    [Theory]
    [InlineData("not-a-mime")]
    [InlineData("text/plain")]
    [InlineData("application/x-a;charset=utf-8")]
    public void SetDragDrop_RejectsInvalidCustomTypes(string type)
    {
        using var testable = new TestableHermesWindow();

        Assert.Throws<ArgumentException>(() => testable.Window.SetDragDrop(true, [type]));
    }

    [Fact]
    public void ApplyTo_CarriesDragDropAndCustomTypes()
    {
        using var testable = new TestableHermesWindow();

        HermesWindowOptions.ApplyTo(testable.Window, new HermesWindowOptions
        {
            DragDrop = true,
            DropCustomTypes = ["application/x-hermes-test"],
        });
        testable.Window.Show();

        Assert.True(testable.Backend.InitialOptions!.DragDrop);
        Assert.Equal(new[] { "application/x-hermes-test" }, testable.Backend.InitialOptions.DropCustomTypes);
    }
}
