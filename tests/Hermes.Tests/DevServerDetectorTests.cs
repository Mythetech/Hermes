// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Blazor.DevServer;
using Xunit;

namespace Hermes.Tests;

public sealed class DevServerDetectorTests
{
    [Fact]
    public void ShouldUseDevServer_NoEnvVar_NoOverride_ReturnsFalse()
    {
        var result = DevServerDetector.ShouldUseDevServer(forceDevServer: null, getEnvVar: _ => null);
        Assert.False(result);
    }

    [Fact]
    public void ShouldUseDevServer_DotnetWatchSet_ReturnTrue()
    {
        var result = DevServerDetector.ShouldUseDevServer(forceDevServer: null, getEnvVar: name =>
            name == "DOTNET_WATCH" ? "1" : null);
        Assert.True(result);
    }

    [Fact]
    public void ShouldUseDevServer_DotnetWatchNotOne_ReturnsFalse()
    {
        var result = DevServerDetector.ShouldUseDevServer(forceDevServer: null, getEnvVar: name =>
            name == "DOTNET_WATCH" ? "0" : null);
        Assert.False(result);
    }

    [Fact]
    public void ShouldUseDevServer_ForceTrue_OverridesEnvVar()
    {
        var result = DevServerDetector.ShouldUseDevServer(forceDevServer: true, getEnvVar: _ => null);
        Assert.True(result);
    }

    [Fact]
    public void ShouldUseDevServer_ForceFalse_OverridesEnvVar()
    {
        var result = DevServerDetector.ShouldUseDevServer(forceDevServer: false, getEnvVar: name =>
            name == "DOTNET_WATCH" ? "1" : null);
        Assert.False(result);
    }
}
