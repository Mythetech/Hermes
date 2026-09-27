// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.Diagnostics;

/// <summary>
/// The smoke test switches, read once from the HERMES_SMOKE_TEST* environment variables.
/// This is the only place those variables are read; every layer above asks this class.
/// </summary>
public static class HermesSmokeTest
{
    private static readonly Lazy<SmokeTestSettings> _settings =
        new(() => SmokeTestSettings.FromEnvironment(Environment.GetEnvironmentVariable));

    /// <summary>Whether smoke mode is on (HERMES_SMOKE_TEST=1).</summary>
    public static bool IsEnabled => _settings.Value.IsEnabled;

    /// <summary>Total time budget for the run (HERMES_SMOKE_TEST_TIMEOUT in seconds, default 60).</summary>
    public static TimeSpan Timeout => _settings.Value.Timeout;

    /// <summary>Where the verdict is also written as JSON (HERMES_SMOKE_TEST_RESULT), or null.</summary>
    public static string? ResultPath => _settings.Value.ResultPath;

    /// <summary>Whether the app exits after printing the verdict. False only when HERMES_SMOKE_TEST_EXIT=0.</summary>
    public static bool ExitWhenDone => _settings.Value.ExitWhenDone;

    // Consumed by the separately versioned Hermes and Hermes.Blazor packages through InternalsVisibleTo,
    // so it must stay additive-only: never renamed or removed without releasing all three packages together.
    internal static SmokeTestSettings Settings => _settings.Value;
}
