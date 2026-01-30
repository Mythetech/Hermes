using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
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
        // Extend the frame into the client area
        var margins = new MARGINS
        {
            cyTopHeight = _titlebarHeight
        };

        PInvoke.DwmExtendFrameIntoClientArea(_hwnd, in margins);

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
        PInvoke.DwmExtendFrameIntoClientArea(_hwnd, in margins);
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
                    var monitor = PInvoke.MonitorFromWindow(_hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
                    MONITORINFO mi = default;
                    mi.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();

                    if (PInvoke.GetMonitorInfo(monitor, ref mi))
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

        // Check resize borders first (only when not maximized)
        if (!isMaximized)
        {
            // Top-left corner
            if (relX < resizeBorder && relY < resizeBorder)
                return new LRESULT(PInvoke.HTTOPLEFT);

            // Top-right corner
            if (relX >= width - resizeBorder && relY < resizeBorder)
                return new LRESULT(PInvoke.HTTOPRIGHT);

            // Bottom-left corner
            if (relX < resizeBorder && relY >= height - resizeBorder)
                return new LRESULT(PInvoke.HTBOTTOMLEFT);

            // Bottom-right corner
            if (relX >= width - resizeBorder && relY >= height - resizeBorder)
                return new LRESULT(PInvoke.HTBOTTOMRIGHT);

            // Left edge
            if (relX < resizeBorder)
                return new LRESULT(PInvoke.HTLEFT);

            // Right edge
            if (relX >= width - resizeBorder)
                return new LRESULT(PInvoke.HTRIGHT);

            // Top edge (but not in titlebar caption button area)
            if (relY < resizeBorder)
            {
                // Allow resize at very top except over caption buttons
                int captionButtonsWidth = ScaleForDpi(CaptionButtonWidth * 3); // Close + Max + Min
                if (relX < width - captionButtonsWidth)
                    return new LRESULT(PInvoke.HTTOP);
            }

            // Bottom edge
            if (relY >= height - resizeBorder)
                return new LRESULT(PInvoke.HTBOTTOM);
        }

        // Check if in titlebar area
        if (relY < _titlebarHeight)
        {
            // Caption buttons are handled by DWM - we return HTCAPTION or specific button hits
            // The DWM will paint and handle the actual buttons, we just need to define regions

            int captionButtonsWidth = ScaleForDpi(CaptionButtonWidth * 3);
            int captionButtonsStart = width - captionButtonsWidth;

            // System menu icon area (far left)
            int iconWidth = ScaleForDpi(46);
            if (relX < iconWidth)
            {
                return new LRESULT(PInvoke.HTSYSMENU);
            }

            // Caption buttons area (far right)
            if (relX >= captionButtonsStart)
            {
                // Let DWM handle caption button clicks
                // We need to return specific hit test values for the buttons
                int buttonIndex = (relX - captionButtonsStart) / ScaleForDpi(CaptionButtonWidth);

                return buttonIndex switch
                {
                    0 => new LRESULT(PInvoke.HTMINBUTTON), // Minimize
                    1 => new LRESULT(PInvoke.HTMAXBUTTON), // Maximize/Restore
                    _ => new LRESULT(PInvoke.HTCLOSE),     // Close
                };
            }

            // Draggable titlebar area
            return new LRESULT(PInvoke.HTCAPTION);
        }

        // Client area
        handled = false;
        return new LRESULT(PInvoke.HTCLIENT);
    }

    private int ScaleForDpi(int value)
    {
        return (int)((value * _dpi) / 96.0);
    }
}
