// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.Diagnostics;

internal sealed record SmokeTestSettings(bool IsEnabled, TimeSpan Timeout, string? ResultPath, bool ExitWhenDone)
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    internal static SmokeTestSettings FromEnvironment(Func<string, string?> getVariable)
    {
        var isEnabled = getVariable("HERMES_SMOKE_TEST") == "1";
        var timeout = int.TryParse(getVariable("HERMES_SMOKE_TEST_TIMEOUT"), out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
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
