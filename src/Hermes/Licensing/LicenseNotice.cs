// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Licensing;

internal static class LicenseNotice
{
    private const string Yellow = "\x1b[33m";
    private const string Reset = "\x1b[0m";

    internal static void PrintUnlicensedWarning()
    {
        WriteWarning("[Hermes] No license key configured.");
        WriteWarning("[Hermes] Get a free key at https://mythetech.com/hermes/license");
        WriteWarning("[Hermes] Commercial use (>=$1M revenue) requires a paid license.");
    }

    internal static void PrintValidationWarning(LicenseValidationResult result)
    {
        WriteWarning($"[Hermes] License validation failed: {result.Message}");
        WriteWarning("[Hermes] Your app will continue to run, but this may indicate a configuration issue.");
        WriteWarning("[Hermes] Visit https://mythetech.com/hermes/license for help.");
    }

    private static void WriteWarning(string message)
    {
        Console.Error.WriteLine($"{Yellow}{message}{Reset}");
    }
}
