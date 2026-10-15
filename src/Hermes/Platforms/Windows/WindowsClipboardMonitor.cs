// Copyright (c) Mythetech. Licensed under the MIT License.
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

/// <summary>
/// Listens for clipboard changes on its own thread and message loop, so it works without any Hermes window.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsClipboardMonitor : IDisposable
{
    private const string WindowClassName = "HermesClipboardMonitor";
    private const uint WmClipboardUpdate = PInvoke.WM_CLIPBOARDUPDATE;
    private const uint WmStop = PInvoke.WM_USER + 1;

    // HWND_MESSAGE is a macro cast ((HWND)-3) in winuser.h, which CsWin32 cannot emit as a constant.
    private static readonly HWND s_messageOnlyParent = new(-3);
    private static readonly WNDPROC s_wndProc = WindowProc;
    private static readonly object s_lock = new();
    private static readonly Dictionary<HWND, WindowsClipboardMonitor> s_instances = new();
    private static bool s_classRegistered;
    private static HINSTANCE s_hInstance;

    private readonly Action _changed;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private Exception? _startError;
    private HWND _hwnd;
    private bool _disposed;

    public WindowsClipboardMonitor(Action changed)
    {
        _changed = changed;
        _thread = new Thread(Run) { IsBackground = true, Name = "Hermes Clipboard Monitor" };
        _thread.Start();
        _ready.Wait();

        if (_startError is not null)
            throw new InvalidOperationException("Clipboard monitoring could not start.", _startError);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Never joins: the last unsubscribe can come from a Changed handler running on this thread.
        if (!_hwnd.IsNull)
            PInvoke.PostMessage(_hwnd, WmStop, 0, 0);
        _ready.Dispose();
    }

    private unsafe void Run()
    {
        try
        {
            EnsureWindowClassRegistered();
            fixed (char* className = WindowClassName)
            {
                _hwnd = PInvoke.CreateWindowEx(0, className, className, 0, 0, 0, 0, 0,
                    s_messageOnlyParent, HMENU.Null, s_hInstance, null);
            }

            if (_hwnd.IsNull)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create the clipboard monitor window");

            lock (s_lock)
                s_instances[_hwnd] = this;

            if (!PInvoke.AddClipboardFormatListener(_hwnd))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AddClipboardFormatListener failed");
        }
        catch (Exception ex)
        {
            _startError = ex;
            _ready.Set();
            return;
        }

        _ready.Set();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(in msg);
            PInvoke.DispatchMessage(in msg);
        }
    }

    private void RaiseChanged()
    {
        try
        {
            _changed();
        }
        catch (Exception ex)
        {
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private static unsafe void EnsureWindowClassRegistered()
    {
        lock (s_lock)
        {
            if (s_classRegistered)
                return;

            s_hInstance = PInvoke.GetModuleHandle((PCWSTR)null);
            fixed (char* className = WindowClassName)
            {
                var wcx = new WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = s_wndProc,
                    hInstance = s_hInstance,
                    lpszClassName = className,
                };

                if (PInvoke.RegisterClassEx(in wcx) == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register the clipboard monitor window class");
            }

            s_classRegistered = true;
        }
    }

    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        WindowsClipboardMonitor? instance;
        lock (s_lock)
            s_instances.TryGetValue(hwnd, out instance);

        if (instance is null)
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        switch (msg)
        {
            case WmClipboardUpdate:
                instance.RaiseChanged();
                return new LRESULT(0);
            case WmStop:
                PInvoke.RemoveClipboardFormatListener(hwnd);
                lock (s_lock)
                    s_instances.Remove(hwnd);
                PInvoke.DestroyWindow(hwnd);
                PInvoke.PostQuitMessage(0);
                return new LRESULT(0);
            default:
                return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        }
    }
}
