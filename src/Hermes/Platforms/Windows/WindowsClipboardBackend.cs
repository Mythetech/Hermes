// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Hermes.Platforms.Windows;

/// <summary>Text-only Win32 clipboard until the full Windows backend lands.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsClipboardBackend : IClipboardBackend
{
    private const uint CfUnicodeText = 13;

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public HermesDataFormats GetFormats() =>
        PInvoke.IsClipboardFormatAvailable(CfUnicodeText) ? HermesDataFormats.Text : HermesDataFormats.None;

    public bool HasCustom(string mimeType) => false;

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes) =>
        formats.HasFlag(HermesDataFormats.Text) ? new HermesDataPackage { Text = ReadText() } : new HermesDataPackage();

    public void Write(HermesDataPackage package)
    {
        if (package.Formats != HermesDataFormats.Text || package.Custom.Count > 0)
            throw new NotSupportedException("Only text can be written to the clipboard on Windows in this version.");

        WriteText(package.Text!);
    }

    public void Clear()
    {
        PInvoke.OpenClipboard(default);
        try
        {
            PInvoke.EmptyClipboard();
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public void StartMonitoring()
    {
    }

    public void StopMonitoring()
    {
    }

    public void Dispose()
    {
    }

    private static unsafe void WriteText(string text)
    {
        PInvoke.OpenClipboard(default);
        try
        {
            PInvoke.EmptyClipboard();

            int byteCount = (text.Length + 1) * sizeof(char);
            var hMem = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)byteCount);
            if (hMem.IsNull)
                throw new InvalidOperationException("GlobalAlloc failed.");

            var ptr = PInvoke.GlobalLock(hMem);
            if (ptr == null)
            {
                PInvoke.GlobalFree(hMem);
                throw new InvalidOperationException("GlobalLock failed.");
            }

            fixed (char* src = text)
            {
                Buffer.MemoryCopy(src, ptr, byteCount, text.Length * sizeof(char));
            }
            ((char*)ptr)[text.Length] = '\0';
            PInvoke.GlobalUnlock(hMem);

            var result = PInvoke.SetClipboardData(CfUnicodeText, new HANDLE(hMem.Value));
            if (result.IsNull)
                PInvoke.GlobalFree(hMem);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    private static unsafe string? ReadText()
    {
        if (!PInvoke.IsClipboardFormatAvailable(CfUnicodeText))
            return null;

        PInvoke.OpenClipboard(default);
        try
        {
            var hMem = PInvoke.GetClipboardData(CfUnicodeText);
            if (hMem.IsNull)
                return null;

            var globalHandle = new HGLOBAL(hMem.Value);
            var ptr = PInvoke.GlobalLock(globalHandle);
            if (ptr == null)
                return null;

            try
            {
                var text = new string((char*)ptr);
                return string.IsNullOrEmpty(text) ? null : text;
            }
            finally
            {
                PInvoke.GlobalUnlock(globalHandle);
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }
}
