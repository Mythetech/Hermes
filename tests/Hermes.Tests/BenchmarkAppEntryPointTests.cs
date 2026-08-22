// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests;

/// <summary>
/// Guards the benchmark test apps against losing their [STAThread] entry points.
/// Windows requires the main thread to be STA for WebView2; without the attribute
/// every Windows benchmark iteration fails at window creation. Top-level statement
/// rewrites silently drop the attribute, which is exactly what these tests catch.
/// </summary>
public sealed class BenchmarkAppEntryPointTests
{
    [Theory]
    [InlineData("HermesTestApp")]
    [InlineData("PhotinoTestApp")]
    [InlineData("PhotinoXTestApp")]
    public void BenchmarkApp_EntryPoint_HasStaThreadAttribute(string appName)
    {
        var programPath = Path.Combine(
            FindRepositoryRoot(), "benchmarks", "Hermes.Benchmarks.Apps", appName, "Program.cs");

        Assert.True(File.Exists(programPath), $"Expected benchmark app entry point at {programPath}");

        var source = File.ReadAllText(programPath);

        Assert.True(source.Contains("[STAThread]"),
            $"{appName}/Program.cs has no [STAThread] attribute. Windows requires an STA main " +
            "thread for WebView2; without it every Windows benchmark iteration fails at window " +
            "creation. Keep an explicit Main method with [STAThread], never top-level statements.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hermes.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test base directory");
    }
}
