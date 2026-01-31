using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsCustomTitlebar
{
    private const int DefaultTitlebarHeight = 32;
    private const int ResizeBorderWidth = 6;

    // Caption button widths (approximate, system will handle actual rendering)
    private const int CaptionButtonWidth = 46;

    private readonly HWND _hwnd;
    private int _titlebarHeight;
    private int _dpi;

    public int TitlebarHeight => _titlebarHeight;

    public WindowsCustomTitlebar(HWND hwnd)
    {
        _hwnd = hwnd;
        _dpi = (int)PInvoke.GetDpiForWindow(hwnd);
        if (_dpi == 0) _dpi = 96;
        _titlebarHeight = ScaleForDpi(DefaultTitlebarHeight);
    }

    public void Initialize()
    {
        // No DWM frame extension - WebView fills entire client area
        // The app renders its own titlebar via HTML/Blazor
        var margins = new MARGINS
        {
            cyTopHeight = 0
        };

        unsafe
        {
            DwmExtendFrameIntoClientArea((nint)_hwnd.Value, in margins);
        }

        // Apply dark mode based on system setting
        WindowsTheme.ApplyDarkModeToWindow(_hwnd, WindowsTheme.IsDarkMode);
    }

    public void UpdateDpi(int newDpi)
    {
        _dpi = newDpi;
        _titlebarHeight = ScaleForDpi(DefaultTitlebarHeight);

        // Re-extend frame with new margins
        var margins = new MARGINS
        {
            cyTopHeight = _titlebarHeight
        };
        unsafe
        {
            DwmExtendFrameIntoClientArea((nint)_hwnd.Value, in margins);
        }
    }

    public LRESULT HandleNcCalcSize(WPARAM wParam, LPARAM lParam)
    {
        // wParam == TRUE means lParam points to NCCALCSIZE_PARAMS
        if (wParam.Value != 0)
        {
            unsafe
            {
                var pParams = (NCCALCSIZE_PARAMS*)lParam.Value;

                // Save the original top for maximized adjustment
                var originalTop = pParams->rgrc._0.top;

                // Let DefWindowProc calculate the client rect
                PInvoke.DefWindowProc(_hwnd, PInvoke.WM_NCCALCSIZE, wParam, lParam);

                // Reset top to remove the standard frame
                pParams->rgrc._0.top = originalTop;

                // When maximized, adjust for the window extending past screen edges
                if (PInvoke.IsZoomed(_hwnd))
                {
                    // Get the monitor's work area to constrain the window
                    var monitor = MonitorFromWindow((nint)_hwnd.Value, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                    MONITORINFO mi = default;
                    mi.cbSize = (uint)sizeof(MONITORINFO);

                    if (GetMonitorInfo(monitor, ref mi))
                    {
                        // When maximized, Windows extends the window beyond screen edges
                        // by the resize border width. We need to account for this.
                        pParams->rgrc._0.top = mi.rcWork.top;
                    }
                }
            }
        }

        return new LRESULT(0);
    }

    public LRESULT HandleNcHitTest(LPARAM lParam, out bool handled)
    {
        handled = true;

        // Extract cursor position from lParam
        int x = (short)(lParam.Value & 0xFFFF);
        int y = (short)((lParam.Value >> 16) & 0xFFFF);

        // Get window rect in screen coordinates
        PInvoke.GetWindowRect(_hwnd, out var windowRect);

        // Convert to window-relative coordinates
        int relX = x - windowRect.left;
        int relY = y - windowRect.top;

        int width = windowRect.right - windowRect.left;
        int height = windowRect.bottom - windowRect.top;

        bool isMaximized = PInvoke.IsZoomed(_hwnd);
        int resizeBorder = isMaximized ? 0 : ScaleForDpi(ResizeBorderWidth);

        // Check resize borders (only when not maximized)
        if (!isMaximized)
        {
            // Top-left corner
            if (relX < resizeBorder && relY < resizeBorder)
                return new LRESULT((nint)PInvoke.HTTOPLEFT);

            // Top-right corner
            if (relX >= width - resizeBorder && relY < resizeBorder)
                return new LRESULT((nint)PInvoke.HTTOPRIGHT);

            // Bottom-left corner
            if (relX < resizeBorder && relY >= height - resizeBorder)
                return new LRESULT((nint)PInvoke.HTBOTTOMLEFT);

            // Bottom-right corner
            if (relX >= width - resizeBorder && relY >= height - resizeBorder)
                return new LRESULT((nint)PInvoke.HTBOTTOMRIGHT);

            // Left edge
            if (relX < resizeBorder)
                return new LRESULT((nint)PInvoke.HTLEFT);

            // Right edge
            if (relX >= width - resizeBorder)
                return new LRESULT((nint)PInvoke.HTRIGHT);

            // Top edge
            if (relY < resizeBorder)
                return new LRESULT((nint)PInvoke.HTTOP);

            // Bottom edge
            if (relY >= height - resizeBorder)
                return new LRESULT((nint)PInvoke.HTBOTTOM);
        }

        // Everything else goes to WebView (client area)
        // The app uses -webkit-app-region: drag for draggable titlebar regions
        // and renders its own window controls
        handled = false;
        return new LRESULT((nint)PInvoke.HTCLIENT);
    }

    private int ScaleForDpi(int value)
    {
        return (int)((value * _dpi) / 96.0);
    }

    // Manual P/Invoke definitions for types not generated by CsWin32 in .NET 10
    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    internal enum MONITOR_FROM_FLAGS : uint
    {
        MONITOR_DEFAULTTONULL = 0x00000000,
        MONITOR_DEFAULTTOPRIMARY = 0x00000001,
        MONITOR_DEFAULTTONEAREST = 0x00000002,
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmExtendFrameIntoClientArea(nint hWnd, in MARGINS pMarInset);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint MonitorFromWindow(nint hwnd, MONITOR_FROM_FLAGS dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);
}
