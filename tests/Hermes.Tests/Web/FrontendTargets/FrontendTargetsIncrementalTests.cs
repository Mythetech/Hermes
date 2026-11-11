// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Web.FrontendTargets;

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

    private void BuildRelease()
    {
        var result = _fixture.Build("Release");
        Assert.True(result.Succeeded, result.Output);
    }
}
