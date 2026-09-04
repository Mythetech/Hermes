// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// The WebView host layer is forked from dotnet/aspnetcore under the MIT license. The
/// license requires attribution, so every forked file must say where it came from and
/// must be listed in THIRD-PARTY-NOTICES.md, and the notices must pin the upstream tag
/// so the fork can be re-diffed at each .NET major.
/// </summary>
public class ForkProvenanceTests
{
    private const string UpstreamHeader = "// The .NET Foundation licenses this file to you under the MIT license.";
    private const string ForkMarker = "// Forked by Mythetech from dotnet/aspnetcore tag v10.0.11";

    [Fact]
    public void EveryForkedFile_CarriesTheForkMarker_AndIsListedInTheNotices()
    {
        var root = FindRepositoryRoot();
        var notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md"));
        Assert.Contains("dotnet/aspnetcore", notices);
        Assert.Contains("v10.0.11", notices);

        var forkedFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Hermes.Blazor", "WebView"), "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadLines(f).Skip(1).FirstOrDefault() == UpstreamHeader)
            .ToList();

        Assert.NotEmpty(forkedFiles);

        foreach (var file in forkedFiles)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            Assert.Contains(ForkMarker, File.ReadAllText(file));
            Assert.Contains(relative, notices);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hermes.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Hermes.sln not found above the test directory.");
    }
}
