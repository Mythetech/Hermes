// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// Reports test results using markers that CI can parse.
/// Extends the existing smoke test pattern with granular test reporting.
/// </summary>
public static class TestReporter
{
    private static readonly List<(string Name, bool Passed, string? Error)> _results = new();
    private static readonly object _lock = new();
    private static bool _readyEmitted;

    /// <summary>
    /// Emit the HERMES_READY marker to signal that the app has initialized.
    /// This is the smoke test marker that CI checks for.
    /// </summary>
    public static void Ready()
    {
        if (_readyEmitted) return;
        _readyEmitted = true;
        Console.WriteLine("HERMES_READY: Integration test app initialized");
    }

    /// <summary>
    /// Report that a test scenario is starting.
    /// </summary>
    public static void Start(string testName)
    {
        Console.WriteLine($"HERMES_TEST_START: {testName}");
    }

    /// <summary>
    /// Report that a test scenario passed.
    /// </summary>
    public static void Pass(string testName)
    {
        lock (_lock)
        {
            _results.Add((testName, true, null));
        }
        Console.WriteLine($"HERMES_TEST_PASS: {testName}");
    }

    /// <summary>
    /// Report that a test scenario failed.
    /// </summary>
    public static void Fail(string testName, string? error = null)
    {
        lock (_lock)
        {
            _results.Add((testName, false, error));
        }
        var message = string.IsNullOrEmpty(error)
            ? $"HERMES_TEST_FAIL: {testName}"
            : $"HERMES_TEST_FAIL: {testName} - {error}";
        Console.WriteLine(message);
    }

    /// <summary>
    /// Report a test result based on a condition.
    /// </summary>
    public static void Assert(string testName, bool condition, string? failureMessage = null)
    {
        if (condition)
            Pass(testName);
        else
            Fail(testName, failureMessage);
    }

    /// <summary>
    /// Print a summary of all test results.
    /// </summary>
    public static void PrintSummary()
    {
        List<(string Name, bool Passed, string? Error)> results;
        lock (_lock)
        {
            results = new List<(string Name, bool Passed, string? Error)>(_results);
        }

        var passed = results.Count(r => r.Passed);
        var failed = results.Count(r => !r.Passed);
        var total = results.Count;

        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           HERMES INTEGRATION TEST RESULTS                  ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");

        foreach (var (name, isPassed, error) in results)
        {
            var status = isPassed ? "PASS" : "FAIL";
            var errorMsg = string.IsNullOrEmpty(error) ? "" : $" ({error})";
            Console.WriteLine($"║  [{status}] {name,-45}{errorMsg} ║");
        }

        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Total: {total}, Passed: {passed}, Failed: {failed}                           ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        if (failed > 0)
        {
            Console.WriteLine($"HERMES_TEST_SUMMARY: FAILED ({failed}/{total} tests failed)");
        }
        else
        {
            Console.WriteLine($"HERMES_TEST_SUMMARY: PASSED ({passed}/{total} tests passed)");
        }
    }

    /// <summary>
    /// Get whether all tests passed.
    /// </summary>
    public static bool AllPassed
    {
        get
        {
            lock (_lock)
            {
                return _results.All(r => r.Passed);
            }
        }
    }

    /// <summary>
    /// Get the count of failed tests.
    /// </summary>
    public static int FailedCount
    {
        get
        {
            lock (_lock)
            {
                return _results.Count(r => !r.Passed);
            }
        }
    }
}
