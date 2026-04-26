// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests;

public class CloseRequestedTests
{
    [Fact]
    public void Close_WithNoHandler_Proceeds()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test");

        testWindow.Show();
        testWindow.Close();

        Assert.True(testWindow.Backend.IsClosed);
    }

    [Fact]
    public void Close_WithHandlerReturningTrue_Proceeds()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test")
            .OnCloseRequested(() => Task.FromResult(true));

        testWindow.Show();
        testWindow.Close();

        Assert.True(testWindow.Backend.IsClosed);
    }

    [Fact]
    public void Close_WithHandlerReturningFalse_Cancels()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test")
            .OnCloseRequested(() => Task.FromResult(false));

        testWindow.Show();
        testWindow.Close();

        Assert.False(testWindow.Backend.IsClosed);
    }

    [Fact]
    public void Close_WithThrowingHandler_ProceedsAndLogs()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test")
            .OnCloseRequested(() => throw new InvalidOperationException("test error"));

        testWindow.Show();
        testWindow.Close();

        Assert.True(testWindow.Backend.IsClosed);
    }

    [Fact]
    public void Close_BypassFlag_DoesNotReFireHandler()
    {
        var callCount = 0;
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test")
            .OnCloseRequested(() =>
            {
                callCount++;
                return Task.FromResult(true);
            });

        testWindow.Show();
        testWindow.Close();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Close_AfterCancel_HandlerFiresAgain()
    {
        var callCount = 0;
        var allowClose = false;

        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test")
            .OnCloseRequested(() =>
            {
                callCount++;
                return Task.FromResult(allowClose);
            });

        testWindow.Show();

        testWindow.Close();
        Assert.Equal(1, callCount);
        Assert.False(testWindow.Backend.IsClosed);

        allowClose = true;
        testWindow.Close();
        Assert.Equal(2, callCount);
        Assert.True(testWindow.Backend.IsClosed);
    }

    [Fact]
    public void Close_ReplacingHandler_UsesNewHandler()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test")
            .OnCloseRequested(() => Task.FromResult(false));

        testWindow.Show();
        testWindow.Close();
        Assert.False(testWindow.Backend.IsClosed);

        testWindow.Window.OnCloseRequested(() => Task.FromResult(true));

        testWindow.Close();
        Assert.True(testWindow.Backend.IsClosed);
    }
}
