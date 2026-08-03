// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowActivation
{
    internal static void ActivateProcessWindow(int processId)
    {
        PInvoke.AllowSetForegroundWindow((uint)processId);

        HWND targetWindow = HWND.Null;

        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (!PInvoke.IsWindowVisible(hwnd))
                return true;

            uint windowPid;
            unsafe
            {
                PInvoke.GetWindowThreadProcessId(hwnd, &windowPid);
            }

            if ((int)windowPid == processId)
            {
                targetWindow = hwnd;
                return false;
            }

            return true;
        }, 0);

        if (targetWindow != HWND.Null)
        {
            PInvoke.SetForegroundWindow(targetWindow);
        }
    }
}
