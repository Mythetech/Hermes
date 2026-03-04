// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hermes.Platforms.Linux;

/// <summary>
/// Native parameter structure for window creation.
/// Must match the C struct HermesWindowParams in HermesTypes.h exactly.
/// </summary>
[SupportedOSPlatform("linux")]
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

    [MarshalAs(UnmanagedType.U1)]
    public bool CustomTitleBar;

    // Explicit padding to align to 8-byte boundary for next pointer field
    // C compiler adds this automatically; C# Sequential layout does not
    // 10 bools = 10 bytes, need 6 more to reach 16 bytes (multiple of 8)
    private byte _pad0;
    private byte _pad1;
    private byte _pad2;
    private byte _pad3;
    private byte _pad4;
    private byte _pad5;

    // Callback function pointers
    public IntPtr OnClosing;
    public IntPtr OnResized;
    public IntPtr OnMoved;
    public IntPtr OnFocusIn;
    public IntPtr OnFocusOut;
    public IntPtr OnWebMessage;
    public IntPtr OnCustomScheme;

    // Custom URL schemes to register (must be set before WebView creation)
    // Fixed-size array of 16 pointers to UTF-8 strings
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public IntPtr[] CustomSchemeNames;
}
