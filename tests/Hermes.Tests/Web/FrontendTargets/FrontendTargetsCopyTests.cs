// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Web.FrontendTargets;

public sealed class FrontendTargetsCopyTests : IDisposable
{
    private readonly FrontendFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void DebugBuild_CopiesExistingOutput_WithoutRunningCommands()
    {
        _fixture.WritePrebuiltDist();

        var result = _fixture.Build("Debug");

        Assert.True(result.Succeeded, result.Output);
        var wwwroot = Path.Combine(_fixture.OutputDirectory("Debug"), "wwwroot");
        Assert.Equal("<html>prebuilt</html>", File.ReadAllText(Path.Combine(wwwroot, "index.html")));
        Assert.True(File.Exists(Path.Combine(wwwroot, "assets", "app.js")));
        Assert.Equal(0, _fixture.InstallRuns);
        Assert.Equal(0, _fixture.BuildRuns);
    }

    [Fact]
    public void ReleaseBuild_InstallsAndBuilds_ThenCopies()
    {
        var result = _fixture.Build("Release");

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal(1, _fixture.InstallRuns);
        Assert.Equal(1, _fixture.BuildRuns);
        var wwwroot = Path.Combine(_fixture.OutputDirectory("Release"), "wwwroot");
        Assert.True(File.Exists(Path.Combine(wwwroot, "index.html")));
        Assert.True(File.Exists(Path.Combine(wwwroot, "assets", "app.js")));
    }

    [Fact]
    public void Publish_BuildsAndCopiesToPublishDirectory()
    {
        var result = _fixture.Publish();

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal(1, _fixture.BuildRuns);
        var publish = Path.Combine(_fixture.OutputDirectory("Release"), "publish");
        Assert.True(File.Exists(Path.Combine(publish, "wwwroot", "index.html")));
        Assert.True(File.Exists(Path.Combine(publish, "wwwroot", "assets", "app.js")));
    }

    [Fact]
    public void CustomRootAndDist_AreUsed()
    {
        Directory.Move(_fixture.Frontend, Path.Combine(_fixture.Root, "web"));

        var result = _fixture.Build("Release", "HermesFrontendRoot=web", "HermesFrontendDist=build");

        Assert.True(result.Succeeded, result.Output);
        Assert.True(File.Exists(Path.Combine(_fixture.Root, "web", "build", "index.html")));
        Assert.True(File.Exists(Path.Combine(_fixture.OutputDirectory("Release"), "wwwroot", "index.html")));
    }
}
