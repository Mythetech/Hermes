// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.Diagnostics;

// Consumed by the separately versioned Hermes and Hermes.Blazor packages through InternalsVisibleTo, so
// it and its members must stay additive-only: never renamed or removed without releasing all three
// packages together.
internal sealed record SmokeTestSettings(bool IsEnabled, TimeSpan Timeout, string? ResultPath, bool ExitWhenDone)
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    // CreateTimer rejects a delay that does not fit its native timer, so a huge configured value is
    // clamped here rather than left to throw in Start, after HERMES_SMOKE_START has already printed.
    private const int MaxTimeoutSeconds = 86400;

    internal static SmokeTestSettings FromEnvironment(Func<string, string?> getVariable)
    {
        var isEnabled = getVariable("HERMES_SMOKE_TEST") == "1";
        var timeout = int.TryParse(getVariable("HERMES_SMOKE_TEST_TIMEOUT"), out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(Math.Min(seconds, MaxTimeoutSeconds))
            : DefaultTimeout;
        var resultPath = getVariable("HERMES_SMOKE_TEST_RESULT");
        var exitWhenDone = getVariable("HERMES_SMOKE_TEST_EXIT") != "0";

        return new SmokeTestSettings(
            isEnabled,
            timeout,
            string.IsNullOrWhiteSpace(resultPath) ? null : resultPath,
            exitWhenDone);
    }
}
