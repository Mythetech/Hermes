// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Contracts.Diagnostics;
using Hermes.Diagnostics;
using Xunit;

namespace Hermes.Tests;

public sealed class CrashInterceptorTests
{
    [Fact]
    public void HermesCrashContext_CanBeConstructed_WithRequiredFields()
    {
        var exception = new HermesExceptionInfo(
            "System.InvalidOperationException",
            "Test error",
            Array.Empty<HermesStackFrame>());

        var platform = new HermesPlatformInfo(
            "TestApp", "1.0.0", "macOS", "14.3", "arm64");

        var context = new HermesCrashContext(
            exception, platform, DateTimeOffset.UtcNow, CrashSource.UnhandledException);

        Assert.Equal("System.InvalidOperationException", context.Exception.ExceptionType);
        Assert.Equal("TestApp", context.Platform.ProductName);
        Assert.Equal(CrashSource.UnhandledException, context.Source);
        Assert.Null(context.AnonymousSessionId);
        Assert.Null(context.AdditionalContext);
    }

    [Fact]
    public void HermesExceptionInfo_SupportsInnerException()
    {
        var inner = new HermesExceptionInfo(
            "System.ArgumentException", "inner", Array.Empty<HermesStackFrame>());

        var outer = new HermesExceptionInfo(
            "System.InvalidOperationException", "outer",
            Array.Empty<HermesStackFrame>(), inner);

        Assert.NotNull(outer.InnerException);
        Assert.Equal("System.ArgumentException", outer.InnerException!.ExceptionType);
    }

    [Fact]
    public void HermesStackFrame_AllFieldsNullable()
    {
        var frame = new HermesStackFrame(null, null, null, null, null);

        Assert.Null(frame.FileName);
        Assert.Null(frame.MethodName);
        Assert.Null(frame.TypeName);
        Assert.Null(frame.LineNumber);
        Assert.Null(frame.ColumnNumber);
    }

    [Fact]
    public void HermesPlatformInfo_OptionalFieldsDefault()
    {
        var platform = new HermesPlatformInfo(
            "TestApp", "1.0.0", "Windows", "10.0.22631", "x64");

        Assert.Null(platform.DotNetVersion);
        Assert.Null(platform.DeviceModel);
    }

    [Fact]
    public void Enable_SetsIsEnabled()
    {
        try
        {
            HermesCrashInterceptor.Enable();
            Assert.True(HermesCrashInterceptor.IsEnabled);
        }
        finally
        {
            HermesCrashInterceptor.Disable();
        }
    }

    [Fact]
    public void Disable_ClearsIsEnabled()
    {
        HermesCrashInterceptor.Enable();
        HermesCrashInterceptor.Disable();
        Assert.False(HermesCrashInterceptor.IsEnabled);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_DoesNotThrow()
    {
        try
        {
            HermesCrashInterceptor.Enable();
            HermesCrashInterceptor.Enable();
            Assert.True(HermesCrashInterceptor.IsEnabled);
        }
        finally
        {
            HermesCrashInterceptor.Disable();
        }
    }

    [Fact]
    public void BuildContext_PopulatesPlatformInfoFromRuntime()
    {
        HermesCrashInterceptor.ProductName = "TestApp";
        HermesCrashInterceptor.ProductVersion = "2.0.0";

        try
        {
            var ex = new InvalidOperationException("test");
            var context = HermesCrashInterceptor.BuildCrashContext(ex, CrashSource.UnhandledException);

            Assert.Equal("TestApp", context.Platform.ProductName);
            Assert.Equal("2.0.0", context.Platform.ProductVersion);
            Assert.Equal(OSInfo.Current.Platform, context.Platform.OperatingSystem);
            Assert.Equal(OSInfo.Current.Architecture, context.Platform.Architecture);
            Assert.NotNull(context.Platform.DotNetVersion);
            Assert.Equal("System.InvalidOperationException", context.Exception.ExceptionType);
            Assert.Equal("test", context.Exception.Message);
            Assert.Equal(CrashSource.UnhandledException, context.Source);
        }
        finally
        {
            HermesCrashInterceptor.ProductName = null;
            HermesCrashInterceptor.ProductVersion = null;
        }
    }

    [Fact]
    public void BuildContext_IncludesInnerException()
    {
        var inner = new ArgumentException("arg error");
        var outer = new InvalidOperationException("outer", inner);

        var context = HermesCrashInterceptor.BuildCrashContext(outer, CrashSource.UnhandledException);

        Assert.NotNull(context.Exception.InnerException);
        Assert.Equal("System.ArgumentException", context.Exception.InnerException!.ExceptionType);
        Assert.Equal("arg error", context.Exception.InnerException.Message);
    }

    [Fact]
    public void BuildContext_IncludesAnonymousSessionId()
    {
        HermesCrashInterceptor.AnonymousSessionId = "session-123";

        try
        {
            var context = HermesCrashInterceptor.BuildCrashContext(
                new Exception("test"), CrashSource.UnhandledException);

            Assert.Equal("session-123", context.AnonymousSessionId);
        }
        finally
        {
            HermesCrashInterceptor.AnonymousSessionId = null;
        }
    }

    [Fact]
    public void BuildContext_NullStackTrace_ReturnsEmptyList()
    {
        var ex = new Exception("no stack");

        var context = HermesCrashInterceptor.BuildCrashContext(ex, CrashSource.UnhandledException);

        Assert.Empty(context.Exception.StackTrace);
    }

    [Fact]
    public void NotifyCrash_InvokesOnCrashCallback()
    {
        HermesCrashContext? received = null;
        HermesCrashInterceptor.OnCrash = ctx => received = ctx;

        try
        {
            var context = HermesCrashInterceptor.BuildCrashContext(
                new Exception("test notify"), CrashSource.WebViewCrash);

            HermesCrashInterceptor.NotifyCrash(context);

            Assert.NotNull(received);
            Assert.Equal(CrashSource.WebViewCrash, received!.Source);
            Assert.Equal("test notify", received.Exception.Message);
        }
        finally
        {
            HermesCrashInterceptor.OnCrash = null;
        }
    }

    [Fact]
    public void NotifyCrash_OnCrashNull_DoesNotThrow()
    {
        HermesCrashInterceptor.OnCrash = null;

        var context = HermesCrashInterceptor.BuildCrashContext(
            new Exception("test"), CrashSource.UnhandledException);

        HermesCrashInterceptor.NotifyCrash(context);
    }

    [Fact]
    public void NotifyCrash_OnCrashThrows_DoesNotPropagate()
    {
        HermesCrashInterceptor.OnCrash = _ => throw new Exception("handler error");

        try
        {
            var context = HermesCrashInterceptor.BuildCrashContext(
                new Exception("test"), CrashSource.UnhandledException);

            HermesCrashInterceptor.NotifyCrash(context);
        }
        finally
        {
            HermesCrashInterceptor.OnCrash = null;
        }
    }

    [Fact]
    public void BuildContext_WebViewCrash_SetsCorrectSource()
    {
        var context = HermesCrashInterceptor.BuildCrashContext(
            new InvalidOperationException("WebView process terminated"),
            CrashSource.WebViewCrash);

        Assert.Equal(CrashSource.WebViewCrash, context.Source);
        Assert.Equal("System.InvalidOperationException", context.Exception.ExceptionType);
    }

    [Fact]
    public void BuildContext_ProductNameNull_DefaultsToUnknown()
    {
        HermesCrashInterceptor.ProductName = null;
        HermesCrashInterceptor.ProductVersion = null;

        var context = HermesCrashInterceptor.BuildCrashContext(
            new Exception("test"), CrashSource.UnhandledException);

        Assert.Equal("Unknown", context.Platform.ProductName);
        Assert.Equal("0.0.0", context.Platform.ProductVersion);
    }

}
