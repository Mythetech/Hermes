// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;

namespace Hermes.Benchmarks.Harness;

public class Program
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);

    // Sampling memory a fixed interval after first render keeps readings comparable
    // across frameworks without waiting out the full ready timeout every iteration.
    private static readonly TimeSpan MemorySettleDelay = TimeSpan.FromSeconds(2);

    public static async Task Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Hermes Benchmarks").Color(Color.Purple));
        AnsiConsole.MarkupLine("[dim]Startup & Memory Benchmark Harness[/]");
        AnsiConsole.WriteLine();

        var iterations = 30;
        var warmupIterations = 3;
        var includeTauri = false;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--iterations" && i + 1 < args.Length)
                iterations = int.Parse(args[i + 1]);
            if (args[i] == "--warmup" && i + 1 < args.Length)
                warmupIterations = int.Parse(args[i + 1]);
            if (args[i] == "--tauri")
                includeTauri = true;
        }

        // Find the test app executables
        var basePath = FindBenchmarkAppsPath();
        if (basePath == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find benchmark apps. Build them first with 'dotnet build -c Release'[/]");
            return;
        }

        var apps = new List<AppDefinition>
        {
            new("Hermes", GetDotnetAppPath(basePath, "HermesTestApp"), "blue",
                "dotnet build -c Release benchmarks/Hermes.Benchmarks.Apps/HermesTestApp"),
            new("HermesFast", GetDotnetAppPath(basePath, "HermesTestApp"), "cyan1",
                "dotnet build -c Release benchmarks/Hermes.Benchmarks.Apps/HermesTestApp", "--fast"),
            new("Photino", GetDotnetAppPath(basePath, "PhotinoTestApp"), "green",
                "dotnet build -c Release benchmarks/Hermes.Benchmarks.Apps/PhotinoTestApp"),
            new("PhotinoX", GetDotnetAppPath(basePath, "PhotinoXTestApp"), "mediumpurple2",
                "dotnet build -c Release benchmarks/Hermes.Benchmarks.Apps/PhotinoXTestApp"),
        };

        if (includeTauri)
        {
            apps.Add(new AppDefinition("Tauri", GetTauriAppPath(basePath), "orange1",
                "cd benchmarks/Hermes.Benchmarks.Apps/TauriTestApp && dotnet publish BlazorApp -c Release -o dist && cargo tauri build"));
        }

        foreach (var app in apps)
        {
            if (!File.Exists(app.Path))
            {
                AnsiConsole.MarkupLine($"[red]{app.Name} app not found at: {app.Path}[/]");
                AnsiConsole.MarkupLine($"[yellow]Build with: {app.BuildHint}[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[dim]{app.Name} app: {app.Path}[/]");
        }

        AnsiConsole.MarkupLine($"[dim]Iterations: {iterations} (warmup: {warmupIterations})[/]");
        AnsiConsole.WriteLine();

        // Run benchmarks
        var appResults = new List<AppBenchmarkResults>();
        foreach (var app in apps)
        {
            appResults.Add(await RunStartupBenchmark(app, iterations, warmupIterations));
        }

        // Display results
        DisplayResults(apps, appResults);

        // Export results
        var results = new BenchmarkResults
        {
            Timestamp = DateTime.UtcNow,
            Environment = GetEnvironmentInfo(),
            Hermes = appResults.FirstOrDefault(r => r.Name == "Hermes"),
            HermesFast = appResults.FirstOrDefault(r => r.Name == "HermesFast"),
            Photino = appResults.FirstOrDefault(r => r.Name == "Photino"),
            PhotinoX = appResults.FirstOrDefault(r => r.Name == "PhotinoX"),
            Tauri = appResults.FirstOrDefault(r => r.Name == "Tauri")
        };

        var jsonPath = "benchmark-results.json";
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        AnsiConsole.MarkupLine($"[dim]Results exported to: {jsonPath}[/]");
    }

    private static async Task<AppBenchmarkResults> RunStartupBenchmark(AppDefinition app, int iterations, int warmupIterations)
    {
        var results = new AppBenchmarkResults { Name = app.Name };
        var startupTimes = new List<double>();
        var memoryReadings = new List<long>();

        await AnsiConsole.Status()
            .StartAsync($"Running {app.Name} benchmarks...", async ctx =>
            {
                // Warmup runs (not counted)
                ctx.Status($"[yellow]Warming up {app.Name}...[/]");
                for (int i = 0; i < warmupIterations; i++)
                {
                    await RunSingleIteration(app);
                }

                // Actual benchmark runs
                for (int i = 0; i < iterations; i++)
                {
                    ctx.Status($"[{app.Color}]{app.Name}[/] iteration {i + 1}/{iterations}");

                    var (startupTime, peakMemory) = await RunSingleIteration(app);

                    if (startupTime.HasValue)
                        startupTimes.Add(startupTime.Value);
                    if (peakMemory.HasValue)
                        memoryReadings.Add(peakMemory.Value);
                }
            });

        // Raw samples stay in iteration order so exported results show distribution
        // shape and whether fast runs cluster after warmup or scatter randomly
        results.StartupSamplesMs = startupTimes.ToList();
        results.MemorySamplesMB = memoryReadings.Select(m => m / (1024.0 * 1024.0)).ToList();

        if (startupTimes.Count > 0)
        {
            var sortedStartupTimes = startupTimes.OrderBy(t => t).ToList();
            results.StartupTimeMs = new Statistics
            {
                Mean = sortedStartupTimes.Average(),
                Median = sortedStartupTimes[sortedStartupTimes.Count / 2],
                Min = sortedStartupTimes.Min(),
                Max = sortedStartupTimes.Max(),
                StdDev = CalculateStdDev(sortedStartupTimes),
                P95 = sortedStartupTimes[(int)(sortedStartupTimes.Count * 0.95)],
                SampleCount = sortedStartupTimes.Count
            };
        }

        if (memoryReadings.Count > 0)
        {
            var sortedMemoryReadings = memoryReadings.OrderBy(m => m).ToList();
            results.PeakMemoryMB = new Statistics
            {
                Mean = sortedMemoryReadings.Average() / (1024.0 * 1024.0),
                Median = sortedMemoryReadings[sortedMemoryReadings.Count / 2] / (1024.0 * 1024.0),
                Min = sortedMemoryReadings.Min() / (1024.0 * 1024.0),
                Max = sortedMemoryReadings.Max() / (1024.0 * 1024.0),
                StdDev = CalculateStdDev(sortedMemoryReadings.Select(m => (double)m).ToList()) / (1024.0 * 1024.0),
                P95 = sortedMemoryReadings[(int)(sortedMemoryReadings.Count * 0.95)] / (1024.0 * 1024.0),
                SampleCount = sortedMemoryReadings.Count
            };
        }

        return results;
    }

    private static async Task<(double? StartupTime, long? PeakMemory)> RunSingleIteration(AppDefinition app)
    {
        var psi = new ProcessStartInfo(app.Path)
        {
            Arguments = app.Args ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        using var process = Process.Start(psi);
        if (process == null) return (null, null);

        var readySignal = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null && e.Data.StartsWith("BENCHMARK_READY:"))
            {
                var timeStr = e.Data.Substring("BENCHMARK_READY:".Length).Trim();
                if (double.TryParse(timeStr, out var time))
                    readySignal.TrySetResult(time);
            }
        };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        double? startupTime = null;
        var completed = await Task.WhenAny(readySignal.Task, Task.Delay(ReadyTimeout));
        if (completed == readySignal.Task)
        {
            startupTime = await readySignal.Task;
            await Task.Delay(MemorySettleDelay);
        }

        long? peakMemory = null;
        if (!process.HasExited)
        {
            try
            {
                process.Refresh();
                // PeakWorkingSet64 doesn't work well on macOS, try multiple approaches
                peakMemory = process.PeakWorkingSet64;
                if (peakMemory == 0)
                    peakMemory = process.WorkingSet64;

                // On macOS, use ps as fallback
                if (peakMemory == 0 && OperatingSystem.IsMacOS())
                {
                    try
                    {
                        var psInfo = new ProcessStartInfo("ps", $"-o rss= -p {process.Id}")
                        {
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        };
                        using var psProc = Process.Start(psInfo);
                        if (psProc != null)
                        {
                            var rss = await psProc.StandardOutput.ReadToEndAsync();
                            if (long.TryParse(rss.Trim(), out var rssKb))
                                peakMemory = rssKb * 1024; // Convert KB to bytes
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // Kill the process after measuring
            try { process.Kill(); } catch { }
        }

        try
        {
            using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(exitCts.Token);
        }
        catch (OperationCanceledException) { }

        return (startupTime, peakMemory);
    }

    private static void DisplayResults(List<AppDefinition> apps, List<AppBenchmarkResults> results)
    {
        var baseline = results.FirstOrDefault(r => r.Name == "Hermes");
        if (baseline == null) return;

        DisplayMetricTable(
            "Startup Time Results", "ms", apps, results, baseline,
            r => r.StartupTimeMs, lowerIsBetter: true);

        DisplayMetricTable(
            "Memory Results", "MB", apps, results, baseline,
            r => r.PeakMemoryMB, lowerIsBetter: true);
    }

    private static void DisplayMetricTable(
        string title,
        string unit,
        List<AppDefinition> apps,
        List<AppBenchmarkResults> results,
        AppBenchmarkResults baseline,
        Func<AppBenchmarkResults, Statistics?> metric,
        bool lowerIsBetter)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold yellow]{title}[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var comparisons = results.Where(r => r.Name != baseline.Name && metric(r) != null).ToList();
        var baselineStats = metric(baseline);
        if (baselineStats == null) return;

        var table = new Table().Border(TableBorder.Rounded).AddColumn("Metric");

        foreach (var result in results)
        {
            var color = apps.First(a => a.Name == result.Name).Color;
            table.AddColumn($"[{color}]{result.Name}[/]");
        }

        foreach (var comparison in comparisons)
            table.AddColumn($"Delta (H vs {comparison.Name})");

        AddStatRow(table, "Mean", unit, results, comparisons, metric, s => s.Mean, baselineStats, lowerIsBetter);
        AddStatRow(table, "Median", unit, results, comparisons, metric, s => s.Median);
        AddStatRow(table, "Min", unit, results, comparisons, metric, s => s.Min);
        AddStatRow(table, "Max", unit, results, comparisons, metric, s => s.Max);
        AddStatRow(table, "StdDev", unit, results, comparisons, metric, s => s.StdDev);
        AddStatRow(table, "P95", unit, results, comparisons, metric, s => s.P95);

        var samplesRow = new List<string> { "Samples" };
        samplesRow.AddRange(results.Select(r => metric(r)?.SampleCount.ToString() ?? "-"));
        samplesRow.AddRange(comparisons.Select(_ => ""));
        table.AddRow(samplesRow.ToArray());

        AnsiConsole.Write(table);
    }

    private static void AddStatRow(
        Table table,
        string label,
        string unit,
        List<AppBenchmarkResults> results,
        List<AppBenchmarkResults> comparisons,
        Func<AppBenchmarkResults, Statistics?> metric,
        Func<Statistics, double> value,
        Statistics? baselineStats = null,
        bool lowerIsBetter = true)
    {
        var row = new List<string> { label };

        foreach (var result in results)
        {
            var stats = metric(result);
            row.Add(stats == null ? "-" : $"{value(stats):F2} {unit}");
        }

        foreach (var comparison in comparisons)
        {
            if (baselineStats == null)
            {
                row.Add("");
                continue;
            }

            var comparisonStats = metric(comparison)!;
            var delta = ((value(baselineStats) - value(comparisonStats)) / value(comparisonStats)) * 100;
            var isGood = lowerIsBetter ? delta < 0 : delta > 0;
            var color = isGood ? "green" : "red";
            row.Add($"[{color}]{delta:+0.0;-0.0}%[/]");
        }

        table.AddRow(row.ToArray());
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

    private static string GetDotnetAppPath(string basePath, string appName)
    {
        return Path.Combine(basePath, appName, "bin", "Release", "net10.0", GetExecutableName(appName));
    }

    private static string GetTauriAppPath(string basePath)
    {
        // Tauri builds to target/release/ directory
        var tauriDir = Path.Combine(basePath, "TauriTestApp", "src-tauri", "target", "release");

        if (OperatingSystem.IsWindows())
            return Path.Combine(tauriDir, "tauri-test-app.exe");
        else if (OperatingSystem.IsMacOS())
            return Path.Combine(tauriDir, "tauri-test-app");
        else
            return Path.Combine(tauriDir, "tauri-test-app");
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

public record AppDefinition(string Name, string Path, string Color, string BuildHint, string? Args = null);

// Data classes for results
public class BenchmarkResults
{
    public DateTime Timestamp { get; set; }
    public EnvironmentInfo? Environment { get; set; }
    public AppBenchmarkResults? Hermes { get; set; }
    public AppBenchmarkResults? HermesFast { get; set; }
    public AppBenchmarkResults? Photino { get; set; }
    public AppBenchmarkResults? PhotinoX { get; set; }
    public AppBenchmarkResults? Tauri { get; set; }
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
    public List<double>? StartupSamplesMs { get; set; }
    public List<double>? MemorySamplesMB { get; set; }
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
