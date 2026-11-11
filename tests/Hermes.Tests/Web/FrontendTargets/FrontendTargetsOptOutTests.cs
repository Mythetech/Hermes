// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Web.FrontendTargets;

public sealed class FrontendTargetsOptOutTests : IDisposable
{
    private readonly FrontendFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void HermesFrontendBuildFalse_SkipsCommandsInRelease()
    {
        var result = _fixture.Build("Release", "HermesFrontendBuild=false");

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal((0, 0), (_fixture.InstallRuns, _fixture.BuildRuns));
        Assert.False(Directory.Exists(Path.Combine(_fixture.OutputDirectory("Release"), "wwwroot")));
    }

    [Fact]
    public void HermesFrontendBuildTrue_RunsCommandsInDebug()
    {
        var result = _fixture.Build("Debug", "HermesFrontendBuild=true");

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal((1, 1), (_fixture.InstallRuns, _fixture.BuildRuns));
    }

    [Fact]
    public void DesignTimeBuild_NeverRunsCommands()
    {
        var result = _fixture.Build("Release", "DesignTimeBuild=true");

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal((0, 0), (_fixture.InstallRuns, _fixture.BuildRuns));
    }

    [Fact]
    public void PublishWithoutBuilding_NeverRunsCommands()
    {
        Assert.True(_fixture.Build("Release", "HermesFrontendBuild=false").Succeeded);

        var result = _fixture.PublishWithoutBuilding();

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal((0, 0), (_fixture.InstallRuns, _fixture.BuildRuns));
    }

    [Fact]
    public void ProjectWithoutFrontend_Builds()
    {
        Directory.Delete(_fixture.Frontend, recursive: true);

        var result = _fixture.Build("Release");

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal((0, 0), (_fixture.InstallRuns, _fixture.BuildRuns));
        Assert.False(Directory.Exists(Path.Combine(_fixture.OutputDirectory("Release"), "wwwroot")));
    }

    [Fact]
    public void FailingCommand_FailsBuild_AndNamesTheOptOut()
    {
        var result = _fixture.Build("Release", "HermesFrontendBuildCommand=exit 3");

        Assert.False(result.Succeeded, result.Output);
        Assert.Contains("HERMESWEB001", result.Output);
        Assert.Contains("exit code 3", result.Output);
        Assert.Contains("HermesFrontendBuild=false", result.Output);
    }
}
