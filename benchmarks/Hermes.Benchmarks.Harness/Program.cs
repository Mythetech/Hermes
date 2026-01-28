using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;

namespace Hermes.Benchmarks.Harness;

public class Program
{
    public static async Task Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Hermes vs Photino").Color(Color.Purple));
        AnsiConsole.MarkupLine("[dim]Startup & Memory Benchmark Harness[/]");
        AnsiConsole.WriteLine();

        var iterations = 30;
        var warmupIterations = 3;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--iterations" && i + 1 < args.Length)
                iterations = int.Parse(args[i + 1]);
            if (args[i] == "--warmup" && i + 1 < args.Length)
                warmupIterations = int.Parse(args[i + 1]);
        }

        // Find the test app executables
        var basePath = FindBenchmarkAppsPath();
        if (basePath == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find benchmark apps. Build them first with 'dotnet build -c Release'[/]");
            return;
        }

        var hermesAppPath = Path.Combine(basePath, "HermesTestApp", "bin", "Release", "net10.0", GetExecutableName("HermesTestApp"));
        var photinoAppPath = Path.Combine(basePath, "PhotinoTestApp", "bin", "Release", "net10.0", GetExecutableName("PhotinoTestApp"));

        if (!File.Exists(hermesAppPath))
        {
            AnsiConsole.MarkupLine($"[red]Hermes app not found at: {hermesAppPath}[/]");
            AnsiConsole.MarkupLine("[yellow]Build with: dotnet build -c Release benchmarks/Hermes.Benchmarks.Apps/HermesTestApp[/]");
            return;
        }

        if (!File.Exists(photinoAppPath))
        {
            AnsiConsole.MarkupLine($"[red]Photino app not found at: {photinoAppPath}[/]");
            AnsiConsole.MarkupLine("[yellow]Build with: dotnet build -c Release benchmarks/Hermes.Benchmarks.Apps/PhotinoTestApp[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Hermes app: {hermesAppPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Photino app: {photinoAppPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Iterations: {iterations} (warmup: {warmupIterations})[/]");
        AnsiConsole.WriteLine();

        // Run benchmarks
        var hermesResults = await RunStartupBenchmark("Hermes", hermesAppPath, iterations, warmupIterations);
        var photinoResults = await RunStartupBenchmark("Photino", photinoAppPath, iterations, warmupIterations);

        // Display results
        DisplayResults(hermesResults, photinoResults);

        // Export results
        var results = new BenchmarkResults
        {
            Timestamp = DateTime.UtcNow,
            Environment = GetEnvironmentInfo(),
            Hermes = hermesResults,
            Photino = photinoResults
        };

        var jsonPath = "benchmark-results.json";
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        AnsiConsole.MarkupLine($"[dim]Results exported to: {jsonPath}[/]");
    }

    private static async Task<AppBenchmarkResults> RunStartupBenchmark(string name, string appPath, int iterations, int warmupIterations)
    {
        var results = new AppBenchmarkResults { Name = name };
        var startupTimes = new List<double>();
        var memoryReadings = new List<long>();

        await AnsiConsole.Status()
            .StartAsync($"Running {name} benchmarks...", async ctx =>
            {
                // Warmup runs (not counted)
                ctx.Status($"[yellow]Warming up {name}...[/]");
                for (int i = 0; i < warmupIterations; i++)
                {
                    await RunSingleIteration(appPath, null, null);
                }

                // Actual benchmark runs
                for (int i = 0; i < iterations; i++)
                {
                    ctx.Status($"[blue]{name}[/] iteration {i + 1}/{iterations}");

                    var (startupTime, peakMemory) = await RunSingleIteration(appPath, startupTimes, memoryReadings);

                    if (startupTime.HasValue)
                        startupTimes.Add(startupTime.Value);
                    if (peakMemory.HasValue)
                        memoryReadings.Add(peakMemory.Value);
                }
            });

        // Calculate statistics
        if (startupTimes.Count > 0)
        {
            startupTimes.Sort();
            results.StartupTimeMs = new Statistics
            {
                Mean = startupTimes.Average(),
                Median = startupTimes[startupTimes.Count / 2],
                Min = startupTimes.Min(),
                Max = startupTimes.Max(),
                StdDev = CalculateStdDev(startupTimes),
                P95 = startupTimes[(int)(startupTimes.Count * 0.95)],
                SampleCount = startupTimes.Count
            };
        }

        if (memoryReadings.Count > 0)
        {
            memoryReadings.Sort();
            results.PeakMemoryMB = new Statistics
            {
                Mean = memoryReadings.Average() / (1024.0 * 1024.0),
                Median = memoryReadings[memoryReadings.Count / 2] / (1024.0 * 1024.0),
                Min = memoryReadings.Min() / (1024.0 * 1024.0),
                Max = memoryReadings.Max() / (1024.0 * 1024.0),
                StdDev = CalculateStdDev(memoryReadings.Select(m => (double)m).ToList()) / (1024.0 * 1024.0),
                P95 = memoryReadings[(int)(memoryReadings.Count * 0.95)] / (1024.0 * 1024.0),
                SampleCount = memoryReadings.Count
            };
        }

        return results;
    }

    private static async Task<(double? StartupTime, long? PeakMemory)> RunSingleIteration(
        string appPath,
        List<double>? startupTimes,
        List<long>? memoryReadings)
    {
        double? startupTime = null;
        long? peakMemory = null;

        var psi = new ProcessStartInfo(appPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        using var process = Process.Start(psi);
        if (process == null) return (null, null);

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for ready signal or timeout
        var readyReceived = false;
        var timeout = TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();

        while (!readyReceived && sw.Elapsed < timeout && !process.HasExited)
        {
            await Task.Delay(100);

            // Check if we got the ready signal
            // Note: For a real implementation, we'd want to read output incrementally
        }

        // Capture memory at this point
        if (!process.HasExited)
        {
            try
            {
                process.Refresh();
                peakMemory = process.PeakWorkingSet64;
            }
            catch { }

            // Kill the process after measuring
            try { process.Kill(); } catch { }
        }

        await Task.WhenAll(outputTask, errorTask);
        var output = await outputTask;

        // Parse the startup time from output
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("BENCHMARK_READY:"))
            {
                var timeStr = line.Substring("BENCHMARK_READY:".Length).Trim();
                if (double.TryParse(timeStr, out var time))
                {
                    startupTime = time;
                }
            }
        }

        return (startupTime, peakMemory);
    }

    private static void DisplayResults(AppBenchmarkResults hermes, AppBenchmarkResults photino)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Startup Time Results[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var startupTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("[blue]Hermes[/]")
            .AddColumn("[green]Photino[/]")
            .AddColumn("Delta");

        if (hermes.StartupTimeMs != null && photino.StartupTimeMs != null)
        {
            var delta = ((hermes.StartupTimeMs.Mean - photino.StartupTimeMs.Mean) / photino.StartupTimeMs.Mean) * 100;
            var deltaColor = delta < 0 ? "green" : "red";

            startupTable.AddRow("Mean", $"{hermes.StartupTimeMs.Mean:F2} ms", $"{photino.StartupTimeMs.Mean:F2} ms", $"[{deltaColor}]{delta:+0.0;-0.0}%[/]");
            startupTable.AddRow("Median", $"{hermes.StartupTimeMs.Median:F2} ms", $"{photino.StartupTimeMs.Median:F2} ms", "");
            startupTable.AddRow("Min", $"{hermes.StartupTimeMs.Min:F2} ms", $"{photino.StartupTimeMs.Min:F2} ms", "");
            startupTable.AddRow("Max", $"{hermes.StartupTimeMs.Max:F2} ms", $"{photino.StartupTimeMs.Max:F2} ms", "");
            startupTable.AddRow("StdDev", $"{hermes.StartupTimeMs.StdDev:F2} ms", $"{photino.StartupTimeMs.StdDev:F2} ms", "");
            startupTable.AddRow("P95", $"{hermes.StartupTimeMs.P95:F2} ms", $"{photino.StartupTimeMs.P95:F2} ms", "");
            startupTable.AddRow("Samples", $"{hermes.StartupTimeMs.SampleCount}", $"{photino.StartupTimeMs.SampleCount}", "");
        }

        AnsiConsole.Write(startupTable);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Memory Results[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var memoryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("[blue]Hermes[/]")
            .AddColumn("[green]Photino[/]")
            .AddColumn("Delta");

        if (hermes.PeakMemoryMB != null && photino.PeakMemoryMB != null)
        {
            var delta = ((hermes.PeakMemoryMB.Mean - photino.PeakMemoryMB.Mean) / photino.PeakMemoryMB.Mean) * 100;
            var deltaColor = delta < 0 ? "green" : "red";

            memoryTable.AddRow("Mean", $"{hermes.PeakMemoryMB.Mean:F2} MB", $"{photino.PeakMemoryMB.Mean:F2} MB", $"[{deltaColor}]{delta:+0.0;-0.0}%[/]");
            memoryTable.AddRow("Median", $"{hermes.PeakMemoryMB.Median:F2} MB", $"{photino.PeakMemoryMB.Median:F2} MB", "");
            memoryTable.AddRow("Min", $"{hermes.PeakMemoryMB.Min:F2} MB", $"{photino.PeakMemoryMB.Min:F2} MB", "");
            memoryTable.AddRow("Max", $"{hermes.PeakMemoryMB.Max:F2} MB", $"{photino.PeakMemoryMB.Max:F2} MB", "");
        }

        AnsiConsole.Write(memoryTable);
    }

    private static double CalculateStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    private static string? FindBenchmarkAppsPath()
    {
        // Try to find the benchmark apps directory
        var current = Directory.GetCurrentDirectory();
        var paths = new[]
        {
            Path.Combine(current, "benchmarks", "Hermes.Benchmarks.Apps"),
            Path.Combine(current, "..", "Hermes.Benchmarks.Apps"),
            Path.Combine(current, "..", "..", "Hermes.Benchmarks.Apps"),
            Path.Combine(current, "..", "..", "..", "benchmarks", "Hermes.Benchmarks.Apps"),
        };

        return paths.FirstOrDefault(Directory.Exists);
    }

    private static string GetExecutableName(string baseName)
    {
        return OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;
    }

    private static EnvironmentInfo GetEnvironmentInfo()
    {
        return new EnvironmentInfo
        {
            OS = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}",
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ProcessorCount = Environment.ProcessorCount,
            MachineName = Environment.MachineName
        };
    }
}

// Data classes for results
public class BenchmarkResults
{
    public DateTime Timestamp { get; set; }
    public EnvironmentInfo? Environment { get; set; }
    public AppBenchmarkResults? Hermes { get; set; }
    public AppBenchmarkResults? Photino { get; set; }
}

public class EnvironmentInfo
{
    public string? OS { get; set; }
    public string? Runtime { get; set; }
    public int ProcessorCount { get; set; }
    public string? MachineName { get; set; }
}

public class AppBenchmarkResults
{
    public string? Name { get; set; }
    public Statistics? StartupTimeMs { get; set; }
    public Statistics? PeakMemoryMB { get; set; }
}

public class Statistics
{
    public double Mean { get; set; }
    public double Median { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double StdDev { get; set; }
    public double P95 { get; set; }
    public int SampleCount { get; set; }
}
