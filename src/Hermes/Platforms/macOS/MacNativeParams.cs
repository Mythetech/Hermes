using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hermes.Platforms.macOS;

/// <summary>
/// Native parameter structure for window creation.
/// Must match the C struct HermesWindowParams in HermesTypes.h exactly.
/// </summary>
[SupportedOSPlatform("macos")]
[StructLayout(LayoutKind.Sequential)]
internal struct HermesWindowParams
{
    // String properties (UTF-8 pointers)
    public IntPtr Title;
    public IntPtr StartUrl;
    public IntPtr StartHtml;
    public IntPtr IconPath;

    // Position and size
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public int MinWidth;
    public int MinHeight;
    public int MaxWidth;
    public int MaxHeight;

    // Boolean flags (marshal as byte for C bool compatibility)
    [MarshalAs(UnmanagedType.U1)]
    public bool UsePosition;

    [MarshalAs(UnmanagedType.U1)]
    public bool CenterOnScreen;

    [MarshalAs(UnmanagedType.U1)]
    public bool Chromeless;

    [MarshalAs(UnmanagedType.U1)]
    public bool Resizable;

    [MarshalAs(UnmanagedType.U1)]
    public bool TopMost;

    [MarshalAs(UnmanagedType.U1)]
    public bool Maximized;

    [MarshalAs(UnmanagedType.U1)]
    public bool Minimized;

    [MarshalAs(UnmanagedType.U1)]
    public bool DevToolsEnabled;

    [MarshalAs(UnmanagedType.U1)]
    public bool ContextMenuEnabled;

    // Callback function pointers
    public IntPtr OnClosing;
    public IntPtr OnResized;
    public IntPtr OnMoved;
    public IntPtr OnFocusIn;
    public IntPtr OnFocusOut;
    public IntPtr OnWebMessage;
    public IntPtr OnCustomScheme;
}
