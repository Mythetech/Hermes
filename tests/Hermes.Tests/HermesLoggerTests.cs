// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Diagnostics;
using Xunit;

namespace Hermes.Tests;

public class HermesLoggerTests : IDisposable
{
    public HermesLoggerTests()
    {
        // Reset logger state before each test
        HermesLogger.LogError = null;
        HermesLogger.LogWarning = null;
        HermesLogger.LogInfo = null;
        HermesLogger.LogDebug = null;
    }

    public void Dispose()
    {
        // Clean up after test
        HermesLogger.LogError = null;
        HermesLogger.LogWarning = null;
        HermesLogger.LogInfo = null;
        HermesLogger.LogDebug = null;
    }

    [Fact]
    public void LogError_WithHandler_InvokesHandler()
    {
        string? capturedMessage = null;
        Exception? capturedException = null;

        HermesLogger.LogError = (msg, ex) =>
        {
            capturedMessage = msg;
            capturedException = ex;
        };

        var testException = new InvalidOperationException("test error");

        // Use reflection to call internal method
        var method = typeof(HermesLogger).GetMethod("Error",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        method!.Invoke(null, new object?[] { "Test message", testException });

        Assert.Equal("Test message", capturedMessage);
        Assert.Same(testException, capturedException);
    }

    [Fact]
    public void LogWarning_WithHandler_InvokesHandler()
    {
        string? capturedMessage = null;

        HermesLogger.LogWarning = msg => capturedMessage = msg;

        var method = typeof(HermesLogger).GetMethod("Warning",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        method!.Invoke(null, new object[] { "Warning message" });

        Assert.Equal("Warning message", capturedMessage);
    }

    [Fact]
    public void LogInfo_WithHandler_InvokesHandler()
    {
        string? capturedMessage = null;

        HermesLogger.LogInfo = msg => capturedMessage = msg;

        var method = typeof(HermesLogger).GetMethod("Info",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        method!.Invoke(null, new object[] { "Info message" });

        Assert.Equal("Info message", capturedMessage);
    }

    [Fact]
    public void LogError_WithoutHandler_DoesNotThrow()
    {
        // Should not throw when no handler is set
        var method = typeof(HermesLogger).GetMethod("Error",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        var exception = Record.Exception(() =>
            method!.Invoke(null, new object?[] { "Test message", null }));

        Assert.Null(exception);
    }

    [Fact]
    public void LogWarning_WithoutHandler_DoesNotThrow()
    {
        var method = typeof(HermesLogger).GetMethod("Warning",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        var exception = Record.Exception(() =>
            method!.Invoke(null, new object[] { "Test message" }));

        Assert.Null(exception);
    }

    [Fact]
    public void Handler_CanBeChangedAtRuntime()
    {
        var messages = new List<string>();

        HermesLogger.LogWarning = msg => messages.Add($"Handler1: {msg}");

        var method = typeof(HermesLogger).GetMethod("Warning",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        method!.Invoke(null, new object[] { "First" });

        HermesLogger.LogWarning = msg => messages.Add($"Handler2: {msg}");
        method!.Invoke(null, new object[] { "Second" });

        Assert.Equal(2, messages.Count);
        Assert.Equal("Handler1: First", messages[0]);
        Assert.Equal("Handler2: Second", messages[1]);
    }
}
