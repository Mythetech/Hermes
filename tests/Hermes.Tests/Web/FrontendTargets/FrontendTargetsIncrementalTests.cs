// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Web.FrontendTargets;

[Collection(nameof(FrontendTargetsCollection))]
public sealed class FrontendTargetsIncrementalTests : IDisposable
{
    private readonly FrontendFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void ReleaseBuild_RunsCommandsOnlyWhenTheirInputsChange()
    {
        BuildRelease();
        BuildRelease();
        Assert.Equal((1, 1), (_fixture.InstallRuns, _fixture.BuildRuns));

        File.SetLastWriteTimeUtc(Path.Combine(_fixture.Frontend, "src", "main.js"), DateTime.UtcNow);
        BuildRelease();
        Assert.Equal((1, 2), (_fixture.InstallRuns, _fixture.BuildRuns));

        File.SetLastWriteTimeUtc(Path.Combine(_fixture.Frontend, "package.json"), DateTime.UtcNow);
        BuildRelease();
        Assert.Equal((2, 3), (_fixture.InstallRuns, _fixture.BuildRuns));
    }

    [Fact]
    public void ReleaseBuild_RebuildsWhatWasDeleted()
    {
        BuildRelease();

        Directory.Delete(Path.Combine(_fixture.Frontend, "dist"), recursive: true);
        BuildRelease();
        Assert.Equal((1, 2), (_fixture.InstallRuns, _fixture.BuildRuns));
        Assert.True(File.Exists(Path.Combine(_fixture.OutputDirectory("Release"), "wwwroot", "index.html")));

        Directory.Delete(Path.Combine(_fixture.Frontend, "node_modules"), recursive: true);
        BuildRelease();
        Assert.Equal(2, _fixture.InstallRuns);
    }

    [Fact]
    public void PublishForARuntime_ReusesTheReleaseBuild()
    {
        BuildRelease();

        var result = _fixture.PublishForThisRuntime();

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal((1, 1), (_fixture.InstallRuns, _fixture.BuildRuns));
        Assert.True(File.Exists(Path.Combine(_fixture.PublishDirectoryForThisRuntime(), "wwwroot", "index.html")));
    }

    [Fact]
    public void MultiTargetedBuild_RunsTheCommandsOnce()
    {
        var result = _fixture.Build("Release", "FixtureMultiTarget=true");

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal((1, 1), (_fixture.InstallRuns, _fixture.BuildRuns));
        foreach (var framework in new[] { "net10.0", "net11.0" })
            Assert.True(File.Exists(Path.Combine(_fixture.Root, "bin", "Release", framework, "wwwroot", "index.html")), framework);
    }

    [Fact]
    public void FrontendAtTheProjectRoot_IsIncremental()
    {
        foreach (var entry in Directory.GetFileSystemEntries(_fixture.Frontend))
            Directory.Move(entry, Path.Combine(_fixture.Root, Path.GetFileName(entry)));
        Directory.Delete(_fixture.Frontend);

        BuildRelease("HermesFrontendRoot=.");
        BuildRelease("HermesFrontendRoot=.");

        Assert.Equal((1, 1), (_fixture.InstallRuns, _fixture.BuildRuns));
        Assert.True(File.Exists(Path.Combine(_fixture.OutputDirectory("Release"), "wwwroot", "index.html")));
    }

    private void BuildRelease(params string[] properties)
    {
        var result = _fixture.Build("Release", properties);
        Assert.True(result.Succeeded, result.Output);
    }
}
