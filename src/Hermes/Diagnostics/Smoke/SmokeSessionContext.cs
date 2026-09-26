// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Diagnostics.Smoke;

/// <summary>
/// Everything a smoke session touches outside itself, so the whole run can be driven in tests.
/// </summary>
internal sealed class SmokeSessionContext
{
    internal required TimeProvider Time { get; init; }

    internal required TextWriter Output { get; init; }

    internal required SmokeAppInfo App { get; init; }

    internal required bool IsMacOS { get; init; }

    internal required Action<int> SetExitCode { get; init; }

    internal required Action<int> HardExit { get; init; }

    internal required Action<string, string> WriteFile { get; init; }

    internal static SmokeSessionContext ForCurrentProcess() => new()
    {
        Time = TimeProvider.System,
        Output = Console.Out,
        App = SmokeAppInfo.FromCurrentProcess(),
        IsMacOS = OperatingSystem.IsMacOS(),
        SetExitCode = code => Environment.ExitCode = code,
        HardExit = Environment.Exit,
        WriteFile = WriteAllTextCreatingDirectory,
    };

    private static void WriteAllTextCreatingDirectory(string path, string contents)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, contents);
    }
}
