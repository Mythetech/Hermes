// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hermes.Platforms.macOS;

/// <summary>
/// Callback delegate definitions for macOS native interop.
/// These delegates are called from native code and must be kept alive
/// during the window's lifetime to prevent garbage collection.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacNativeDelegates
{
    /// <summary>
    /// Called when the window is about to close.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ClosingCallback();

    /// <summary>
    /// Called when the window is resized.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ResizedCallback(int width, int height);

    /// <summary>
    /// Called when the window is moved.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MovedCallback(int x, int y);

    /// <summary>
    /// Called when the window gains or loses focus.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void FocusCallback();

    /// <summary>
    /// Called when a message is received from JavaScript.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void WebMessageCallback(IntPtr messagePtr);

    /// <summary>
    /// Called when a custom URL scheme request is received.
    /// Returns pointer to response data, sets numBytes and contentType.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr CustomSchemeCallback(IntPtr urlPtr, out int numBytes, out IntPtr contentTypePtr);

    /// <summary>
    /// Called when a menu item is clicked.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MenuItemCallback(IntPtr itemIdPtr);

    /// <summary>
    /// Callback for Invoke/BeginInvoke operations.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void InvokeCallback();
}
