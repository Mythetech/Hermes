// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Text;

namespace Hermes.Tests.Web.FrontendTargets;

/// <summary>
/// A temp copy of tests/FrontendTargetsFixture, built with the real dotnet CLI against the Hermes.Web targets.
/// </summary>
internal sealed class FrontendFixture : IDisposable
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(3);

    public FrontendFixture()
    {
        var root = FindRepositoryRoot();
        TargetsPath = Path.Combine(root, "src", "Hermes.Web", "build", "Mythetech.Hermes.Web.targets");
        Root = Directory.CreateTempSubdirectory("hermes-frontend-targets-").FullName;
        CopyDirectory(Path.Combine(root, "tests", "FrontendTargetsFixture"), Root);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(Root, "global.json"));
    }

    public string Root { get; }

    public string TargetsPath { get; }

    public string Frontend => Path.Combine(Root, "frontend");

    public int InstallRuns => CountLines("install.log");

    public int BuildRuns => CountLines("build.log");

    public BuildResult Build(string configuration, params string[] properties) => Run("build", configuration, properties);

    public BuildResult Publish(params string[] properties) => Run("publish", "Release", properties);

    public BuildResult PublishWithoutBuilding() => Run("publish", "Release", [], "--no-build");

    /// <summary>The single target framework folder under bin/{configuration}.</summary>
    public string OutputDirectory(string configuration) =>
        Directory.GetDirectories(Path.Combine(Root, "bin", configuration)).Single();

    public void WritePrebuiltDist(string dist = "dist")
    {
        var assets = Directory.CreateDirectory(Path.Combine(Frontend, dist, "assets"));
        File.WriteAllText(Path.Combine(Frontend, dist, "index.html"), "<html>prebuilt</html>");
        File.WriteAllText(Path.Combine(assets.FullName, "app.js"), "console.log('prebuilt');");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private BuildResult Run(string verb, string configuration, string[] properties, params string[] options)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in new[] { verb, "FrontendTargetsFixture.csproj", "-c", configuration, "--disable-build-servers", "-nologo", $"-p:HermesWebTargets={TargetsPath}" })
            start.ArgumentList.Add(argument);
        foreach (var property in properties)
            start.ArgumentList.Add($"-p:{property}");
        foreach (var option in options)
            start.ArgumentList.Add(option);

        // The test host inherits the outer build's MSBuild variables, which would point the child build at it.
        foreach (var name in start.Environment.Keys.Where(static k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(name);

        var output = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(BuildTimeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {verb} did not finish within {BuildTimeout.TotalMinutes} minutes:\n{output}");
        }

        process.WaitForExit();
        lock (output)
            return new BuildResult(process.ExitCode, output.ToString());
    }

    private int CountLines(string log)
    {
        var path = Path.Combine(Root, log);
        return File.Exists(path) ? File.ReadAllLines(path).Count(static line => line.Trim().Length > 0) : 0;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.Ordinal));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.StartsWith("bin", StringComparison.Ordinal) || relative.StartsWith("obj", StringComparison.Ordinal))
                continue;
            File.Copy(file, Path.Combine(destination, relative));
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hermes.sln")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not find Hermes.sln above the test output directory.");
    }
}

internal sealed record BuildResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}
