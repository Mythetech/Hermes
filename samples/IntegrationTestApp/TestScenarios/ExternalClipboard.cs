// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;

namespace IntegrationTestApp.TestScenarios;

/// <summary>Writes the clipboard from another process, so change monitoring sees a foreign write.</summary>
internal static class ExternalClipboard
{
    public static void WriteText(string text)
    {
        var (fileName, arguments) = OperatingSystem.IsMacOS() ? ("pbcopy", "")
            : OperatingSystem.IsWindows() ? ("powershell", "-NoProfile -Command \"$input | Set-Clipboard\"")
            : ("xclip", "-selection clipboard");

        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
        })!;
        process.StandardInput.Write(text);
        process.StandardInput.Close();

        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            throw new TimeoutException($"{fileName} did not exit within 10s");
        }
    }
}
