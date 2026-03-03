// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.Versioning;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsTheme
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDarkMode
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
                var value = key?.GetValue("AppsUseLightTheme");
                // AppsUseLightTheme: 0 = dark mode, 1 = light mode
                return value is int intValue && intValue == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool TransparencyEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
                var value = key?.GetValue("EnableTransparency");
                return value is int intValue && intValue == 1;
            }
            catch
            {
                return true;
            }
        }
    }

    public static uint AccentColor
    {
        get
        {
            unsafe
            {
                uint colorization;
                BOOL opaqueBlend;
                if (PInvoke.DwmGetColorizationColor(&colorization, &opaqueBlend).Succeeded)
                {
                    return colorization;
                }
            }
            return 0x0078D4FF; // Default Windows blue
        }
    }

    public static string AccentColorHex
    {
        get
        {
            var color = AccentColor;
            // DwmGetColorizationColor returns ARGB
            var r = (color >> 16) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = color & 0xFF;
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }

    public static void ApplyDarkModeToWindow(HWND hwnd, bool useDarkMode)
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Windows 10 20H1+)
        // For older Windows 10, it was attribute 19
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        unsafe
        {
            int value = useDarkMode ? 1 : 0;

            // Try the newer attribute first
            var result = PInvoke.DwmSetWindowAttribute(
                hwnd,
                (DWMWINDOWATTRIBUTE)DWMWA_USE_IMMERSIVE_DARK_MODE,
                &value,
                sizeof(int));

            // Fall back to older attribute if that failed
            if (result.Failed)
            {
                PInvoke.DwmSetWindowAttribute(
                    hwnd,
                    (DWMWINDOWATTRIBUTE)DWMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                    &value,
                    sizeof(int));
            }
        }
    }

    public static void ApplyMicaEffect(HWND hwnd, bool enable)
    {
        // DWMWA_SYSTEMBACKDROP_TYPE = 38 (Windows 11 22H2+)
        // Values: 0 = Auto, 1 = None, 2 = Mica, 3 = Acrylic, 4 = Mica Alt
        const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        unsafe
        {
            int value = enable ? 2 : 0; // 2 = Mica
            PInvoke.DwmSetWindowAttribute(
                hwnd,
                (DWMWINDOWATTRIBUTE)DWMWA_SYSTEMBACKDROP_TYPE,
                &value,
                sizeof(int));
        }
    }
}
