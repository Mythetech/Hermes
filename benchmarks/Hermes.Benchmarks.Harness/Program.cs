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
        var windowTimes = new List<double>();
        var memoryReadings = new List<long>();
        var failures = 0;
        string? firstFailure = null;

        // Buffered instead of written live because writing through AnsiConsole
        // while a Status display is active garbles the output
        var failureLog = new List<string>();
        const int MaxDetailedFailures = 3;

        void RecordFailure(string label, IterationResult iteration, bool includeDetail)
        {
            var reason = iteration.TimedOut
                ? "no ready signal within timeout (process still running)"
                : $"process exited with code {iteration.ExitCode} before ready";
            failureLog.Add($"{app.Name} {label} failed: {reason}");

            if (!includeDetail)
                return;

            foreach (var line in iteration.StdErrTail)
                failureLog.Add($"  stderr> {line}");
            if (iteration.StdErrTail.Count == 0)
            {
                foreach (var line in iteration.StdOutTail)
                    failureLog.Add($"  stdout> {line}");
            }
        }

        await AnsiConsole.Status()
            .StartAsync($"Running {app.Name} benchmarks...", async ctx =>
            {
                // Warmup runs (not counted)
                ctx.Status($"[yellow]Warming up {app.Name}...[/]");
                for (int i = 0; i < warmupIterations; i++)
                {
                    var warmup = await RunSingleIteration(app);
                    if (!warmup.StartupTimeMs.HasValue)
                        RecordFailure($"warmup {i + 1}", warmup, includeDetail: true);
                }

                // Actual benchmark runs
                for (int i = 0; i < iterations; i++)
                {
                    ctx.Status($"[{app.Color}]{app.Name}[/] iteration {i + 1}/{iterations}");

                    var iteration = await RunSingleIteration(app);

                    if (iteration.StartupTimeMs.HasValue)
                    {
                        startupTimes.Add(iteration.StartupTimeMs.Value);
                        if (iteration.WindowTimeMs.HasValue)
                            windowTimes.Add(iteration.WindowTimeMs.Value);
                    }
                    else
                    {
                        failures++;
                        firstFailure ??= DescribeFailure(iteration);
                        RecordFailure($"iteration {i + 1}", iteration, includeDetail: failures <= MaxDetailedFailures);
                    }

                    if (iteration.PeakMemoryBytes.HasValue)
                        memoryReadings.Add(iteration.PeakMemoryBytes.Value);
                }
            });

        // Plain Console.WriteLine: AnsiConsole wraps long lines at the console
        // width, which mangles stack traces in redirected CI logs
        foreach (var line in failureLog)
            Console.WriteLine(line);

        results.FailedIterations = failures;
        results.FirstFailure = firstFailure;

        // Raw samples stay in iteration order so exported results show distribution
        // shape and whether fast runs cluster after warmup or scatter randomly
        results.StartupSamplesMs = startupTimes.ToList();
        results.WindowSamplesMs = windowTimes.ToList();
        results.MemorySamplesMB = memoryReadings.Select(m => m / (1024.0 * 1024.0)).ToList();

        if (startupTimes.Count > 0)
            results.StartupTimeMs = ComputeStatistics(startupTimes);

        if (windowTimes.Count > 0)
            results.WindowTimeMs = ComputeStatistics(windowTimes);

        if (memoryReadings.Count > 0)
            results.PeakMemoryMB = ComputeStatistics(results.MemorySamplesMB);

        return results;
    }

    private static Statistics ComputeStatistics(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return new Statistics
        {
            Mean = sorted.Average(),
            Median = sorted[sorted.Count / 2],
            Min = sorted[0],
            Max = sorted[^1],
            StdDev = CalculateStdDev(sorted),
            P95 = sorted[Math.Min((int)(sorted.Count * 0.95), sorted.Count - 1)],
            SampleCount = sorted.Count
        };
    }

    private static async Task<IterationResult> RunSingleIteration(AppDefinition app)
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
        if (process == null)
            return new IterationResult(null, null, null, false, null, ["process failed to start"], []);

        var readySignal = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        var windowSignal = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdoutTail = new OutputBuffer(8, 12);
        var stderrTail = new OutputBuffer(8, 12);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;

            if (e.Data.StartsWith("BENCHMARK_READY:"))
            {
                var timeStr = e.Data.Substring("BENCHMARK_READY:".Length).Trim();
                if (double.TryParse(timeStr, out var time))
                    readySignal.TrySetResult(time);
            }
            else if (e.Data.StartsWith("BENCHMARK_WINDOW:"))
            {
                var timeStr = e.Data.Substring("BENCHMARK_WINDOW:".Length).Trim();
                if (double.TryParse(timeStr, out var time))
                    windowSignal.TrySetResult(time);
            }
            else
            {
                stdoutTail.Add(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stderrTail.Add(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        double? startupTime = null;
        long? peakMemory = null;
        var timedOut = false;
        int? exitCode = null;

        // Waiting on exit too means a crashing app fails the iteration immediately
        // instead of burning the full ready timeout
        var exitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(readySignal.Task, exitTask, Task.Delay(ReadyTimeout));

        if (completed == exitTask && !readySignal.Task.IsCompleted)
        {
            // Give the redirected pipes a moment to drain so the tails capture the crash
            await Task.Delay(250);
        }

        if (readySignal.Task.IsCompleted)
        {
            startupTime = await readySignal.Task;
            await Task.Delay(MemorySettleDelay);

            if (!process.HasExited)
                peakMemory = await SampleMemoryAsync(process);
        }
        else if (process.HasExited)
        {
            exitCode = process.ExitCode;
        }
        else
        {
            timedOut = true;
        }

        if (!process.HasExited)
        {
            try { process.Kill(); } catch { }
        }

        try
        {
            using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(exitCts.Token);
        }
        catch (OperationCanceledException) { }

        // The window signal always precedes ready on the same stdout pipe, so if
        // it fired at all its value is already set by the time we get here
        double? windowTime = windowSignal.Task.IsCompleted ? windowSignal.Task.Result : null;

        return new IterationResult(startupTime, windowTime, peakMemory, timedOut, exitCode, stderrTail.Snapshot(), stdoutTail.Snapshot());
    }

    private static async Task<long?> SampleMemoryAsync(Process process)
    {
        try
        {
            process.Refresh();
            // PeakWorkingSet64 doesn't work well on macOS, try multiple approaches
            long peakMemory = process.PeakWorkingSet64;
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

            return peakMemory > 0 ? peakMemory : null;
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeFailure(IterationResult iteration)
    {
        var reason = iteration.TimedOut
            ? $"no ready signal within {ReadyTimeout.TotalSeconds:F0}s (process still running)"
            : $"process exited with code {iteration.ExitCode} before ready";

        // Head lines, not tail: for .NET crashes the first stderr lines carry
        // the exception type and message, the tail is just stack frames
        var output = string.Join(" | ", iteration.StdErrTail.Take(3));
        if (output.Length == 0)
            output = string.Join(" | ", iteration.StdOutTail.Take(3));

        var detail = output.Length == 0 ? reason : $"{reason}; output: {output}";
        return detail.Length <= 500 ? detail : detail[..500];
    }

    private static void DisplayResults(List<AppDefinition> apps, List<AppBenchmarkResults> results)
    {
        var baseline = results.FirstOrDefault(r => r.Name == "Hermes");
        if (baseline == null) return;

        DisplayMetricTable(
            "Startup Time Results", "ms", apps, results, baseline,
            r => r.StartupTimeMs, lowerIsBetter: true);

        DisplayMetricTable(
            "Window Visible Results", "ms", apps, results, baseline,
            r => r.WindowTimeMs, lowerIsBetter: true);

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

internal sealed record IterationResult(
    double? StartupTimeMs,
    double? WindowTimeMs,
    long? PeakMemoryBytes,
    bool TimedOut,
    int? ExitCode,
    IReadOnlyList<string> StdErrTail,
    IReadOnlyList<string> StdOutTail);

// Keeps the first lines and a ring of the most recent lines: .NET crash dumps
// put "Unhandled exception. Type: message" first, so the head is the part that
// identifies a failure. The stdout/stderr event handlers that feed it fire on
// thread pool threads, so access is locked.
internal sealed class OutputBuffer
{
    private readonly int _headCapacity;
    private readonly int _tailCapacity;
    private readonly List<string> _head = new();
    private readonly Queue<string> _tail = new();
    private readonly object _lock = new();
    private bool _truncated;

    public OutputBuffer(int headCapacity, int tailCapacity)
    {
        _headCapacity = headCapacity;
        _tailCapacity = tailCapacity;
    }

    public void Add(string line)
    {
        lock (_lock)
        {
            if (_head.Count < _headCapacity)
            {
                _head.Add(line);
                return;
            }

            if (_tail.Count == _tailCapacity)
            {
                _tail.Dequeue();
                _truncated = true;
            }
            _tail.Enqueue(line);
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_lock)
        {
            var result = new List<string>(_head);
            if (_truncated)
                result.Add("...");
            result.AddRange(_tail);
            return result;
        }
    }
}

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
    public Statistics? WindowTimeMs { get; set; }
    public Statistics? PeakMemoryMB { get; set; }
    public List<double>? StartupSamplesMs { get; set; }
    public List<double>? WindowSamplesMs { get; set; }
    public List<double>? MemorySamplesMB { get; set; }
    public int FailedIterations { get; set; }
    public string? FirstFailure { get; set; }
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
