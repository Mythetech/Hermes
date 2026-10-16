// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Reflection;
using Hermes.Notifications;
using Xunit;

namespace Hermes.Tests.Notifications;

public sealed class NativeNotificationOptionsTests
{
    [Fact]
    public void Resolve_ExplicitAppId_WinsOverApplicationAppId()
    {
        var options = new NativeNotificationOptions { AppId = "explicit.id" };

        var resolved = options.Resolve("com.example.App");

        Assert.Equal("explicit.id", resolved.AppId);
        Assert.Equal("explicit.id", resolved.DisplayName);
    }

    [Fact]
    public void Resolve_NoExplicitAppId_UsesApplicationAppId()
    {
        var resolved = new NativeNotificationOptions().Resolve("com.example.App");

        Assert.Equal("com.example.App", resolved.AppId);
        Assert.Equal("com.example.App", resolved.DisplayName);
    }

    [Fact]
    public void Resolve_NeitherSet_UsesEntryAssemblyNameOrFallback()
    {
        var resolved = new NativeNotificationOptions().Resolve(null);

        Assert.Equal(Assembly.GetEntryAssembly()?.GetName().Name ?? "HermesApp", resolved.AppId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankExplicitAppId_FallsBackToApplicationAppId(string blank)
    {
        var resolved = new NativeNotificationOptions { AppId = blank }.Resolve("com.example.App");

        Assert.Equal("com.example.App", resolved.AppId);
    }
}
