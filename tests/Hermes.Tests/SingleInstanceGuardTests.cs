// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.SingleInstance;
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
            second.NotifyFirstInstance([$"batch-{i}"]);
        }

        Assert.True(allReceived.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for all notifications");
        Assert.Equal(3, receivedCount);
    }
}
