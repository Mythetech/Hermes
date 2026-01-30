using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;

namespace Hermes.Benchmarks.Harness;

public class Program
{
    public static async Task Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Hermes vs Photino vs Tauri").Color(Color.Purple));
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

        var hermesAppPath = Path.Combine(basePath, "HermesTestApp", "bin", "Release", "net10.0", GetExecutableName("HermesTestApp"));
        var photinoAppPath = Path.Combine(basePath, "PhotinoTestApp", "bin", "Release", "net10.0", GetExecutableName("PhotinoTestApp"));
        var tauriAppPath = GetTauriAppPath(basePath);

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

        if (includeTauri && !File.Exists(tauriAppPath))
        {
            AnsiConsole.MarkupLine($"[red]Tauri app not found at: {tauriAppPath}[/]");
            AnsiConsole.MarkupLine("[yellow]Build with: cd benchmarks/Hermes.Benchmarks.Apps/TauriTestApp && dotnet publish BlazorApp -c Release -o dist && cargo tauri build[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Hermes app: {hermesAppPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Photino app: {photinoAppPath}[/]");
        if (includeTauri)
            AnsiConsole.MarkupLine($"[dim]Tauri app: {tauriAppPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Iterations: {iterations} (warmup: {warmupIterations})[/]");
        AnsiConsole.WriteLine();

        // Run benchmarks
        var hermesResults = await RunStartupBenchmark("Hermes", hermesAppPath, iterations, warmupIterations);
        var photinoResults = await RunStartupBenchmark("Photino", photinoAppPath, iterations, warmupIterations);
        AppBenchmarkResults? tauriResults = null;

        if (includeTauri)
        {
            tauriResults = await RunStartupBenchmark("Tauri", tauriAppPath, iterations, warmupIterations);
        }

        // Display results
        DisplayResults(hermesResults, photinoResults, tauriResults);

        // Export results
        var results = new BenchmarkResults
        {
            Timestamp = DateTime.UtcNow,
            Environment = GetEnvironmentInfo(),
            Hermes = hermesResults,
            Photino = photinoResults,
            Tauri = tauriResults
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

    private static void DisplayResults(AppBenchmarkResults hermes, AppBenchmarkResults photino, AppBenchmarkResults? tauri)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Startup Time Results[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var startupTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("[blue]Hermes[/]")
            .AddColumn("[green]Photino[/]");

        if (tauri != null)
            startupTable.AddColumn("[orange1]Tauri[/]");

        startupTable.AddColumn("Delta (H vs P)");
        if (tauri != null)
            startupTable.AddColumn("Delta (H vs T)");

        if (hermes.StartupTimeMs != null && photino.StartupTimeMs != null)
        {
            var deltaHP = ((hermes.StartupTimeMs.Mean - photino.StartupTimeMs.Mean) / photino.StartupTimeMs.Mean) * 100;
            var deltaColorHP = deltaHP < 0 ? "green" : "red";

            var row = new List<string>
            {
                "Mean",
                $"{hermes.StartupTimeMs.Mean:F2} ms",
                $"{photino.StartupTimeMs.Mean:F2} ms"
            };

            if (tauri?.StartupTimeMs != null)
            {
                row.Add($"{tauri.StartupTimeMs.Mean:F2} ms");
            }

            row.Add($"[{deltaColorHP}]{deltaHP:+0.0;-0.0}%[/]");

            if (tauri?.StartupTimeMs != null)
            {
                var deltaHT = ((hermes.StartupTimeMs.Mean - tauri.StartupTimeMs.Mean) / tauri.StartupTimeMs.Mean) * 100;
                var deltaColorHT = deltaHT < 0 ? "green" : "red";
                row.Add($"[{deltaColorHT}]{deltaHT:+0.0;-0.0}%[/]");
            }

            startupTable.AddRow(row.ToArray());

            // Add other rows without delta columns for cleanliness
            AddStatRow(startupTable, "Median", hermes.StartupTimeMs.Median, photino.StartupTimeMs.Median, tauri?.StartupTimeMs?.Median, "ms");
            AddStatRow(startupTable, "Min", hermes.StartupTimeMs.Min, photino.StartupTimeMs.Min, tauri?.StartupTimeMs?.Min, "ms");
            AddStatRow(startupTable, "Max", hermes.StartupTimeMs.Max, photino.StartupTimeMs.Max, tauri?.StartupTimeMs?.Max, "ms");
            AddStatRow(startupTable, "StdDev", hermes.StartupTimeMs.StdDev, photino.StartupTimeMs.StdDev, tauri?.StartupTimeMs?.StdDev, "ms");
            AddStatRow(startupTable, "P95", hermes.StartupTimeMs.P95, photino.StartupTimeMs.P95, tauri?.StartupTimeMs?.P95, "ms");
            AddStatRowInt(startupTable, "Samples", hermes.StartupTimeMs.SampleCount, photino.StartupTimeMs.SampleCount, tauri?.StartupTimeMs?.SampleCount);
        }

        AnsiConsole.Write(startupTable);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Memory Results[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var memoryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("[blue]Hermes[/]")
            .AddColumn("[green]Photino[/]");

        if (tauri != null)
            memoryTable.AddColumn("[orange1]Tauri[/]");

        memoryTable.AddColumn("Delta (H vs P)");
        if (tauri != null)
            memoryTable.AddColumn("Delta (H vs T)");

        if (hermes.PeakMemoryMB != null && photino.PeakMemoryMB != null)
        {
            var deltaHP = ((hermes.PeakMemoryMB.Mean - photino.PeakMemoryMB.Mean) / photino.PeakMemoryMB.Mean) * 100;
            var deltaColorHP = deltaHP < 0 ? "green" : "red";

            var row = new List<string>
            {
                "Mean",
                $"{hermes.PeakMemoryMB.Mean:F2} MB",
                $"{photino.PeakMemoryMB.Mean:F2} MB"
            };

            if (tauri?.PeakMemoryMB != null)
            {
                row.Add($"{tauri.PeakMemoryMB.Mean:F2} MB");
            }

            row.Add($"[{deltaColorHP}]{deltaHP:+0.0;-0.0}%[/]");

            if (tauri?.PeakMemoryMB != null)
            {
                var deltaHT = ((hermes.PeakMemoryMB.Mean - tauri.PeakMemoryMB.Mean) / tauri.PeakMemoryMB.Mean) * 100;
                var deltaColorHT = deltaHT < 0 ? "green" : "red";
                row.Add($"[{deltaColorHT}]{deltaHT:+0.0;-0.0}%[/]");
            }

            memoryTable.AddRow(row.ToArray());

            AddStatRow(memoryTable, "Median", hermes.PeakMemoryMB.Median, photino.PeakMemoryMB.Median, tauri?.PeakMemoryMB?.Median, "MB");
            AddStatRow(memoryTable, "Min", hermes.PeakMemoryMB.Min, photino.PeakMemoryMB.Min, tauri?.PeakMemoryMB?.Min, "MB");
            AddStatRow(memoryTable, "Max", hermes.PeakMemoryMB.Max, photino.PeakMemoryMB.Max, tauri?.PeakMemoryMB?.Max, "MB");
        }

        AnsiConsole.Write(memoryTable);
    }

    private static void AddStatRow(Table table, string metric, double hermes, double photino, double? tauri, string unit)
    {
        var row = new List<string> { metric, $"{hermes:F2} {unit}", $"{photino:F2} {unit}" };
        if (tauri.HasValue)
            row.Add($"{tauri.Value:F2} {unit}");
        row.Add(""); // Empty delta column
        if (tauri.HasValue)
            row.Add(""); // Empty delta column for Tauri
        table.AddRow(row.ToArray());
    }

    private static void AddStatRowInt(Table table, string metric, int hermes, int photino, int? tauri)
    {
        var row = new List<string> { metric, $"{hermes}", $"{photino}" };
        if (tauri.HasValue)
            row.Add($"{tauri.Value}");
        row.Add(""); // Empty delta column
        if (tauri.HasValue)
            row.Add(""); // Empty delta column for Tauri
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

// Data classes for results
public class BenchmarkResults
{
    public DateTime Timestamp { get; set; }
    public EnvironmentInfo? Environment { get; set; }
    public AppBenchmarkResults? Hermes { get; set; }
    public AppBenchmarkResults? Photino { get; set; }
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
