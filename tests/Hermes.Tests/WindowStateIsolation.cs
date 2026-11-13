// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.CompilerServices;
using Hermes.Storage;

namespace Hermes.Tests;

internal static class WindowStateIsolation
{
    /// <summary>The window state file this test process writes, instead of the user's own.</summary>
    public static string FilePath { get; } =
        Path.Combine(Path.GetTempPath(), $"hermes-tests-{Environment.ProcessId}", "WindowState.json");

    /// <summary>
    /// Every target framework's test run is its own process on the same machine, and the user's file is
    /// shared by all of them, so each process gets a file of its own before any test can save a window.
    /// </summary>
    [ModuleInitializer]
    internal static void UseAFileOfThisProcess()
    {
        WindowStateStore.Instance.UseFileForTest(FilePath);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteDirectory();
    }

    private static void DeleteDirectory()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(FilePath)!, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
