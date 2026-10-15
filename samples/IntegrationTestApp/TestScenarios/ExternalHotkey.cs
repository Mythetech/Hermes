// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Text;

namespace IntegrationTestApp.TestScenarios;

/// <summary>Presses Ctrl+Alt+Shift+F12 from another process, so a global hotkey sees input it did not generate.</summary>
internal static class ExternalHotkey
{
    private const string WindowsScript = """
        Add-Type -Namespace HermesTest -Name Keys -MemberDefinition '[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);'
        $keys = 0x11, 0x12, 0x10, 0x7B
        foreach ($k in $keys) { [HermesTest.Keys]::keybd_event($k, 0, 0, [System.UIntPtr]::Zero) }
        [array]::Reverse($keys)
        foreach ($k in $keys) { [HermesTest.Keys]::keybd_event($k, 0, 2, [System.UIntPtr]::Zero) }
        """;

    public static void PressCtrlAltShiftF12()
    {
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("powershell", "-NoProfile -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsScript)))
            : ("xdotool", "key ctrl+alt+shift+F12");

        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false })!;
        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            // Not a TimeoutException, so a hung helper is never mistaken for a hotkey that did not fire.
            throw new InvalidOperationException($"{fileName} did not exit within 10s");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}");
    }
}
