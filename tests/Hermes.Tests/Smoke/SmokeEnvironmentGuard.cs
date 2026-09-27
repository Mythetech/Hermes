// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.CompilerServices;

namespace Hermes.Tests.Smoke;

internal static class SmokeEnvironmentGuard
{
    private static readonly string[] SmokeSwitches =
    [
        "HERMES_SMOKE_TEST",
        "HERMES_SMOKE_TEST_TIMEOUT",
        "HERMES_SMOKE_TEST_RESULT",
        "HERMES_SMOKE_TEST_EXIT",
    ];

    /// <summary>
    /// HermesSmokeTest reads these switches once per process, on first access. Inherited from a shell
    /// that exported them, they would turn every builder test into a real smoke run with a hard exit
    /// timer, so they are cleared before any test can trigger that read. Tests that need smoke mode
    /// inject their own session instead.
    /// </summary>
    [ModuleInitializer]
    internal static void ClearInheritedSmokeSwitches()
    {
        foreach (var name in SmokeSwitches)
            Environment.SetEnvironmentVariable(name, null);
    }
}
