// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Reports smoke test milestones to stdout for external test harnesses.
/// Enable by setting HERMES_SMOKE_TEST=1 environment variable.
/// Set HERMES_SMOKE_TEST_EXIT=1 to exit with code 0 after first render.
/// </summary>
public static class SmokeTestReporter
{
    private static readonly bool _isEnabled =
        Environment.GetEnvironmentVariable("HERMES_SMOKE_TEST") == "1";

    private static readonly bool _exitOnSuccess =
        Environment.GetEnvironmentVariable("HERMES_SMOKE_TEST_EXIT") == "1";

    private static readonly bool _isIntegrationTest =
        Environment.GetEnvironmentVariable("HERMES_INTEGRATION_TEST") == "1";

    private static bool _firstRenderReported;

    /// <summary>
    /// Returns true if smoke test mode is enabled via HERMES_SMOKE_TEST=1
    /// </summary>
    public static bool IsEnabled => _isEnabled;

    /// <summary>
    /// Reports that the first Blazor render has completed.
    /// Outputs: HERMES_READY:{elapsed_ms}
    /// If HERMES_SMOKE_TEST_EXIT=1, exits with code 0.
    /// </summary>
    public static void ReportFirstRender(double elapsedMilliseconds)
    {
        if (!_isEnabled || _firstRenderReported) return;
        _firstRenderReported = true;

        Console.WriteLine($"HERMES_READY:{elapsedMilliseconds:F2}");
        Console.Out.Flush();

        if (_exitOnSuccess && !_isIntegrationTest)
        {
            Environment.Exit(0);
        }
    }
}
