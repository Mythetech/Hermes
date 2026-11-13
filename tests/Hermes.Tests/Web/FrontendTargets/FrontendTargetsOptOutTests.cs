// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Web.FrontendTargets;

[Collection(nameof(FrontendTargetsCollection))]
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

    [Fact]
    public void FailingCommand_ShowsItsOutput_UnderTheTerminalLogger()
    {
        File.WriteAllText(Path.Combine(_fixture.Frontend, "failure.txt"), "stub-failure-detail");
        var print = OperatingSystem.IsWindows() ? "type" : "cat";

        var result = _fixture.BuildWithOptions("Release", [$"HermesFrontendBuildCommand={print} failure.txt && exit 1"], "-tl:on");

        Assert.False(result.Succeeded, result.Output);
        Assert.Contains("HERMESWEB001", result.Output);
        Assert.Contains("stub-failure-detail", result.Output);
    }

    [Fact]
    public void MissingCommand_SaysTheShellCouldNotFindIt()
    {
        var result = _fixture.Build("Release", "HermesFrontendBuildCommand=hermes-command-that-does-not-exist");

        Assert.False(result.Succeeded, result.Output);
        Assert.Contains("could not find", result.Output);
    }
}
