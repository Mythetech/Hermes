// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Hermes;

/// <summary>
/// Provides cross-platform clipboard access for text content.
/// </summary>
public static class Clipboard
{
    /// <summary>
    /// Copies text to the system clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <exception cref="ArgumentException">Thrown when text is null or whitespace.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown on unsupported platforms.</exception>
    public static void SetText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (OperatingSystem.IsMacOS())
            SetTextMacOS(text);
        else if (OperatingSystem.IsWindows())
            SetTextWindows(text);
        else if (OperatingSystem.IsLinux())
            SetTextLinux(text);
        else
            throw new PlatformNotSupportedException("Clipboard is not supported on this platform.");
    }

    /// <summary>
    /// Gets text from the system clipboard.
    /// Returns null if the clipboard is empty or does not contain text.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown on unsupported platforms.</exception>
    public static string? GetText()
    {
        if (OperatingSystem.IsMacOS())
            return GetTextMacOS();
        else if (OperatingSystem.IsWindows())
            return GetTextWindows();
        else if (OperatingSystem.IsLinux())
            return GetTextLinux();

        throw new PlatformNotSupportedException("Clipboard is not supported on this platform.");
    }

    [SupportedOSPlatform("macos")]
    private static void SetTextMacOS(string text)
    {
        using var process = Process.Start(new ProcessStartInfo("pbcopy")
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
        })!;
        process.StandardInput.Write(text);
        process.StandardInput.Close();
        process.WaitForExit();
    }

    [SupportedOSPlatform("macos")]
    private static string? GetTextMacOS()
    {
        using var process = Process.Start(new ProcessStartInfo("pbpaste")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        var result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return string.IsNullOrEmpty(result) ? null : result;
    }

#if WINDOWS
    [SupportedOSPlatform("windows")]
    private static unsafe void SetTextWindows(string text)
    {
        Windows.Win32.PInvoke.OpenClipboard(default);
        try
        {
            Windows.Win32.PInvoke.EmptyClipboard();

            int byteCount = (text.Length + 1) * sizeof(char);
            var hMem = Windows.Win32.PInvoke.GlobalAlloc(Windows.Win32.System.Memory.GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)byteCount);
            if (hMem.IsNull)
                throw new InvalidOperationException("GlobalAlloc failed.");

            var ptr = Windows.Win32.PInvoke.GlobalLock(hMem);
            if (ptr == null)
            {
                Windows.Win32.PInvoke.GlobalFree(hMem);
                throw new InvalidOperationException("GlobalLock failed.");
            }

            fixed (char* src = text)
            {
                Buffer.MemoryCopy(src, ptr, byteCount, text.Length * sizeof(char));
            }
            ((char*)ptr)[text.Length] = '\0';
            Windows.Win32.PInvoke.GlobalUnlock(hMem);

            var result = Windows.Win32.PInvoke.SetClipboardData(
                13u /* CF_UNICODETEXT */,
                new Windows.Win32.Foundation.HANDLE(hMem.Value));

            if (result.IsNull)
                Windows.Win32.PInvoke.GlobalFree(hMem);
        }
        finally
        {
            Windows.Win32.PInvoke.CloseClipboard();
        }
    }

    [SupportedOSPlatform("windows")]
    private static unsafe string? GetTextWindows()
    {
        if (!Windows.Win32.PInvoke.IsClipboardFormatAvailable(
                13u /* CF_UNICODETEXT */))
            return null;

        Windows.Win32.PInvoke.OpenClipboard(default);
        try
        {
            var hMem = Windows.Win32.PInvoke.GetClipboardData(
                13u /* CF_UNICODETEXT */);

            if (hMem.IsNull)
                return null;

            var globalHandle = new Windows.Win32.Foundation.HGLOBAL(hMem.Value);
            var ptr = Windows.Win32.PInvoke.GlobalLock(globalHandle);
            if (ptr == null)
                return null;

            try
            {
                var text = new string((char*)ptr);
                return string.IsNullOrEmpty(text) ? null : text;
            }
            finally
            {
                Windows.Win32.PInvoke.GlobalUnlock(globalHandle);
            }
        }
        finally
        {
            Windows.Win32.PInvoke.CloseClipboard();
        }
    }
#else
    [SupportedOSPlatform("windows")]
    private static void SetTextWindows(string text) => throw new PlatformNotSupportedException();

    [SupportedOSPlatform("windows")]
    private static string? GetTextWindows() => throw new PlatformNotSupportedException();
#endif

    [SupportedOSPlatform("linux")]
    private static void SetTextLinux(string text)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("xclip")
            {
                Arguments = "-selection clipboard",
                RedirectStandardInput = true,
                UseShellExecute = false,
            })!;
            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new PlatformNotSupportedException(
                "xclip is required for clipboard support on Linux. Install it with: sudo apt install xclip");
        }
    }

    [SupportedOSPlatform("linux")]
    private static string? GetTextLinux()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("xclip")
            {
                Arguments = "-selection clipboard -o",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            var result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new PlatformNotSupportedException(
                "xclip is required for clipboard support on Linux. Install it with: sudo apt install xclip");
        }
    }
}
