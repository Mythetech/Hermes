// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Hermes.SingleInstance;
using Hermes.Tests.Smoke;
using Xunit;

namespace Hermes.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void FirstInstance_IsFirstInstance_ReturnsTrue()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var guard = new SingleInstanceGuard(id);

        Assert.True(guard.IsFirstInstance);
    }

    [Fact]
    public void SecondGuard_SameId_IsNotFirstInstance()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var first = new SingleInstanceGuard(id);
        using var second = new SingleInstanceGuard(id);

        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void DifferentIds_BothAreFirstInstance()
    {
        var id1 = $"t{Guid.NewGuid().ToString("N")[..8]}";
        var id2 = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var guard1 = new SingleInstanceGuard(id1);
        using var guard2 = new SingleInstanceGuard(id2);

        Assert.True(guard1.IsFirstInstance);
        Assert.True(guard2.IsFirstInstance);
    }

    [Fact]
    public void NotifyFirstInstance_DeliversArgs()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var first = new SingleInstanceGuard(id);
        Assert.True(first.IsFirstInstance);

        string[]? receivedArgs = null;
        var received = new ManualResetEventSlim(false);
        first.SecondInstanceLaunched += args =>
        {
            receivedArgs = args;
            received.Set();
        };

        using var second = new SingleInstanceGuard(id);
        Assert.False(second.IsFirstInstance);
        Assert.True(second.NotifyFirstInstance(["--file", "test.txt"]));

        Assert.True(received.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for args");
        Assert.NotNull(receivedArgs);
        Assert.Equal(["--file", "test.txt"], receivedArgs);
    }

    [Fact]
    public void NotifyFirstInstance_EmptyArgs_Works()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var first = new SingleInstanceGuard(id);

        string[]? receivedArgs = null;
        var received = new ManualResetEventSlim(false);
        first.SecondInstanceLaunched += args =>
        {
            receivedArgs = args;
            received.Set();
        };

        using var second = new SingleInstanceGuard(id);
        Assert.True(second.NotifyFirstInstance([]));

        Assert.True(received.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for args");
        Assert.NotNull(receivedArgs);
        Assert.Empty(receivedArgs);
    }

    [Fact]
    public void NotifyFirstInstance_ArgsWithSpecialChars_Works()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var first = new SingleInstanceGuard(id);

        string[]? receivedArgs = null;
        var received = new ManualResetEventSlim(false);
        first.SecondInstanceLaunched += args =>
        {
            receivedArgs = args;
            received.Set();
        };

        var expectedArgs = new[] { "--path", "C:\\Users\\test user\\file.txt", "--name", "héllo wörld", "--emoji", "\U0001f389" };

        using var second = new SingleInstanceGuard(id);
        Assert.True(second.NotifyFirstInstance(expectedArgs));

        Assert.True(received.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for args");
        Assert.NotNull(receivedArgs);
        Assert.Equal(expectedArgs, receivedArgs);
    }

    [Fact]
    public void Dispose_CleansUp_AllowsNewFirstInstance()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";

        var first = new SingleInstanceGuard(id);
        Assert.True(first.IsFirstInstance);
        first.Dispose();

        using var second = new SingleInstanceGuard(id);
        Assert.True(second.IsFirstInstance);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        var guard = new SingleInstanceGuard(id);

        guard.Dispose();
        guard.Dispose(); // Should not throw
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidApplicationId_Empty_ThrowsArgumentException(string? id)
    {
        Assert.Throws<ArgumentException>(() => new SingleInstanceGuard(id!));
    }

    [Theory]
    [InlineData("app id")]
    [InlineData("app/id")]
    [InlineData("app\\id")]
    [InlineData("app:id")]
    public void InvalidApplicationId_BadChars_ThrowsArgumentException(string id)
    {
        Assert.Throws<ArgumentException>(() => new SingleInstanceGuard(id));
    }

    [Fact]
    public void MultipleNotifications_AllDelivered()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var first = new SingleInstanceGuard(id);

        var receivedCount = 0;
        var allReceived = new ManualResetEventSlim(false);
        first.SecondInstanceLaunched += _ =>
        {
            if (Interlocked.Increment(ref receivedCount) == 3)
                allReceived.Set();
        };

        for (var i = 0; i < 3; i++)
        {
            // Allow the pipe server to restart between connections
            Thread.Sleep(200);
            using var second = new SingleInstanceGuard(id);
            Assert.True(second.NotifyFirstInstance([$"batch-{i}"]), $"Notification {i} failed to send");
        }

        // On Unix delivery waits for a thread pool thread, which loaded CI runners have taken over 8 seconds to free.
        Assert.True(allReceived.Wait(TimeSpan.FromSeconds(30)), "Timed out waiting for all notifications");
        Assert.Equal(3, receivedCount);
    }

    [Fact]
    public async Task Notification_WhilePreviousMessageStillProcessing_IsNotLost()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var first = new SingleInstanceGuard(id);

        // Holding the handler open keeps the server busy with message 1 while
        // message 2 is sent. On Unix the second connection lands in the
        // listening socket's backlog and must survive until the server accepts
        // it; on Windows the client blocks until the busy instance disconnects,
        // so the send must run concurrently and the handler is released while
        // the send is in flight.
        var releaseHandler = new ManualResetEventSlim(false);
        var receivedCount = 0;
        var bothReceived = new ManualResetEventSlim(false);
        first.SecondInstanceLaunched += _ =>
        {
            if (Interlocked.Increment(ref receivedCount) == 2)
                bothReceived.Set();
            releaseHandler.Wait(TimeSpan.FromSeconds(10));
        };

        using (var second = new SingleInstanceGuard(id))
        {
            Assert.True(second.NotifyFirstInstance(["first-message"]), "First notification failed to send");
        }

        SpinWait.SpinUntil(() => Volatile.Read(ref receivedCount) >= 1, TimeSpan.FromSeconds(10));
        Assert.Equal(1, receivedCount);

        var secondSend = Task.Run(() =>
        {
            using var third = new SingleInstanceGuard(id);
            return third.NotifyFirstInstance(["second-message"]);
        });

        await Task.Delay(500);
        releaseHandler.Set();

        Assert.True(await secondSend.WaitAsync(TimeSpan.FromSeconds(10)), "Second notification failed to send");
        Assert.True(bothReceived.Wait(TimeSpan.FromSeconds(10)), "Second notification was lost while the first was being processed");
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public void PipeName_IncludesTheUserScope()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var guard = new SingleInstanceGuard(id);

        // macOS keeps pipes in the per-user $TMPDIR, and a suffix would push long ids past the 104-byte socket path limit.
        var expected = OperatingSystem.IsMacOS() ? $"Hermes_SI_{id}"
            : OperatingSystem.IsWindows() ? $"Hermes_SI_{id}_{Process.GetCurrentProcess().SessionId}"
            : $"Hermes_SI_{id}_{geteuid()}";
        Assert.Equal(expected, guard.PipeName);
    }

    /// <summary>Another user's app with the same id holds the unscoped name in the shared /tmp or pipe namespace.</summary>
    [Fact]
    public void NotifyFirstInstance_IgnoresAPipeWithTheUnscopedName()
    {
        if (OperatingSystem.IsMacOS())
            return;

        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        using var decoy = new NamedPipeServerStream($"Hermes_SI_{id}", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var decoyConnected = decoy.WaitForConnectionAsync();

        using var first = new SingleInstanceGuard(id);
        var received = new ManualResetEventSlim(false);
        first.SecondInstanceLaunched += _ => received.Set();

        using var second = new SingleInstanceGuard(id);
        second.NotifyFirstInstance(["--file", "test.txt"]);

        Assert.True(received.Wait(TimeSpan.FromSeconds(5)), "The first instance never received the args");
        Assert.False(decoyConnected.IsCompleted, "The args went to the unscoped pipe");
    }

    [DllImport("libc")]
    private static extern uint geteuid();

    [Fact]
    public void LongApplicationId_StillForwardsArgs()
    {
        var id = $"com.mythetech.horizon-{Guid.NewGuid().ToString("N")[..8]}";
        using var first = new SingleInstanceGuard(id);
        var received = new ManualResetEventSlim(false);
        first.SecondInstanceLaunched += _ => received.Set();

        using var second = new SingleInstanceGuard(id);

        Assert.True(second.NotifyFirstInstance(["--file", "test.txt"]));
        Assert.True(received.Wait(TimeSpan.FromSeconds(5)), "The first instance never received the args");
    }
}

/// <summary>Holds the whole thread pool, so it runs alone.</summary>
[Collection(nameof(ProcessWideEventsCollection))]
public sealed class SingleInstanceGuardBusyPoolTests
{
    /// <summary>On Unix a busy pool only delays delivery, since the connection waits in the socket's backlog.</summary>
    [Fact]
    public void NotifyFirstInstance_ReachesTheFirstInstanceAgain_WhileTheThreadPoolIsBusy()
    {
        var id = $"t{Guid.NewGuid().ToString("N")[..8]}";
        var firstArrived = new ManualResetEventSlim(false);
        var poolBusy = new ManualResetEventSlim(false);
        var secondArrived = new ManualResetEventSlim(false);
        using var first = new SingleInstanceGuard(id);
        first.SecondInstanceLaunched += args =>
        {
            if (args[0] != "one")
            {
                secondArrived.Set();
                return;
            }

            firstArrived.Set();
            // The listener waits for its next client once this returns, so it starts waiting with the pool full.
            poolBusy.Wait(TimeSpan.FromSeconds(10));
        };

        using (var second = new SingleInstanceGuard(id))
            Assert.True(second.NotifyFirstInstance(["one"]));
        Assert.True(firstArrived.Wait(TimeSpan.FromSeconds(5)), "The first instance never received the first notification");

        bool notified;
        using (new ThreadPoolHold())
        {
            poolBusy.Set();
            using var third = new SingleInstanceGuard(id);
            notified = third.NotifyFirstInstance(["two"]);
        }

        Assert.True(notified, "The second notification could not reach the first instance");
        Assert.True(secondArrived.Wait(TimeSpan.FromSeconds(5)), "The first instance never received the second notification");
    }
}
