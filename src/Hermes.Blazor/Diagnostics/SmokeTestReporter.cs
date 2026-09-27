// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Diagnostics;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Superseded by automatic smoke mode: with HERMES_SMOKE_TEST=1 set, Hermes reports the first render and
/// the verdict itself.
/// </summary>
[Obsolete("Smoke mode is automatic when HERMES_SMOKE_TEST=1. Remove this call; Hermes reports the first render and the verdict itself.")]
public static class SmokeTestReporter
{
    /// <summary>Whether smoke mode is on. Use <see cref="HermesSmokeTest.IsEnabled"/> instead.</summary>
    public static bool IsEnabled => HermesSmokeTest.IsEnabled;

    /// <summary>No longer does anything; kept so existing callers still compile.</summary>
    public static void ReportFirstRender(double elapsedMilliseconds)
    {
    }
}
