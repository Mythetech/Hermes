// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsClipboardBackend : IClipboardBackend
{
    private const uint CfUnicodeText = 13;
    private const uint CfDib = 8;
    private const uint CfHdrop = 15;
    private const uint CfDibV5 = 17;
    private const int DropEffectCopy = 1;
    private const int OpenAttempts = 10;

    private static readonly Lazy<uint> s_htmlFormat = new(() => Register("HTML Format"));
    private static readonly Lazy<uint> s_pngFormat = new(() => Register("PNG"));
    private static readonly Lazy<uint> s_dropEffectFormat = new(() => Register("Preferred DropEffect"));

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public HermesDataFormats GetFormats()
    {
        var formats = HermesDataFormats.None;
        if (IsAvailable(CfUnicodeText))
            formats |= HermesDataFormats.Text;
        if (IsAvailable(s_htmlFormat.Value))
            formats |= HermesDataFormats.Html;
        if (IsAvailable(s_pngFormat.Value) || IsAvailable(CfDibV5) || IsAvailable(CfDib))
            formats |= HermesDataFormats.Png;
        if (IsAvailable(CfHdrop))
            formats |= HermesDataFormats.Files;
        return formats;
    }

    public bool HasCustom(string mimeType) => IsAvailable(Register(mimeType));

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes)
    {
        using var scope = ClipboardScope.Open();

        string? text = null;
        string? html = null;
        ReadOnlyMemory<byte>? png = null;
        IReadOnlyList<string>? files = null;

        if (formats.HasFlag(HermesDataFormats.Text) && GetData(CfUnicodeText) is { } textBytes)
            text = DecodeUtf16(textBytes);
        if (formats.HasFlag(HermesDataFormats.Html) && GetData(s_htmlFormat.Value) is { } htmlBytes)
            html = CfHtmlFormat.Parse(htmlBytes);
        if (formats.HasFlag(HermesDataFormats.Png) && ReadPng() is { } pngBytes)
            png = new ReadOnlyMemory<byte>(pngBytes);
        if (formats.HasFlag(HermesDataFormats.Files) && GetData(CfHdrop) is { } dropBytes)
            files = DropFilesFormat.Parse(dropBytes) is { Count: > 0 } paths ? paths : null;

        var custom = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mimeType in customTypes)
        {
            if (GetData(Register(mimeType)) is { } data)
                custom[mimeType] = data;
        }

        return new HermesDataPackage { Text = text, Html = html, Png = png, Files = files, Custom = custom };
    }

    public void Write(HermesDataPackage package)
    {
        // Decode before opening the clipboard so other apps are not locked out while WIC works.
        var legacyDib = package.Png is { } pngToDecode ? CreateLegacyDib(pngToDecode.Span) : null;

        using var scope = ClipboardScope.Open();
        if (!PInvoke.EmptyClipboard())
            throw new IOException("The clipboard could not be emptied.");

        if (package.Text is { } text)
            SetData(CfUnicodeText, EncodeUtf16(text));
        if (package.Html is { } html)
            SetData(s_htmlFormat.Value, WithTerminator(CfHtmlFormat.Build(html)));
        if (package.Png is { } png)
        {
            SetData(s_pngFormat.Value, png.Span);
            if (legacyDib is not null)
                SetData(CfDib, legacyDib);
        }
        if (package.Files is { } files)
        {
            SetData(CfHdrop, DropFilesFormat.Build(files));
            Span<byte> effect = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(effect, DropEffectCopy);
            SetData(s_dropEffectFormat.Value, effect);
        }
        foreach (var (mimeType, data) in package.Custom)
            SetData(Register(mimeType), data.Span);
    }

    public void Clear()
    {
        using var scope = ClipboardScope.Open();
        PInvoke.EmptyClipboard();
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

    private static byte[]? CreateLegacyDib(ReadOnlySpan<byte> png)
    {
        // The legacy bitmap is best effort: an undecodable or inconsistent image just skips it.
        if (!WicPngDecoder.TryDecodeToBgra(png, out var width, out var height, out var bgra)
            || width <= 0 || height <= 0 || (long)width * height * 4 != bgra.Length)
            return null;

        return DibFormat.FromBgra(width, height, bgra);
    }

    private static byte[]? ReadPng()
    {
        if (GetData(s_pngFormat.Value) is { } png)
            return png;

        var dib = GetData(CfDibV5) ?? GetData(CfDib);
        return dib is null ? null : DibFormat.ToPng(dib);
    }

    private static bool IsAvailable(uint format) => format != 0 && PInvoke.IsClipboardFormatAvailable(format);

    private static unsafe byte[]? GetData(uint format)
    {
        if (!IsAvailable(format))
            return null;

        var handle = PInvoke.GetClipboardData(format);
        if (handle.IsNull)
            return null;

        var global = new HGLOBAL(handle.Value);
        var size = (int)PInvoke.GlobalSize(global);
        var pointer = PInvoke.GlobalLock(global);
        if (pointer == null)
            return null;

        try
        {
            return new ReadOnlySpan<byte>(pointer, size).ToArray();
        }
        finally
        {
            PInvoke.GlobalUnlock(global);
        }
    }

    private static unsafe void SetData(uint format, ReadOnlySpan<byte> data)
    {
        var global = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)Math.Max(data.Length, 1));
        if (global.IsNull)
            throw new OutOfMemoryException("GlobalAlloc failed for clipboard data.");

        var pointer = PInvoke.GlobalLock(global);
        if (pointer == null)
        {
            PInvoke.GlobalFree(global);
            throw new IOException("GlobalLock failed for clipboard data.");
        }

        data.CopyTo(new Span<byte>(pointer, data.Length));
        PInvoke.GlobalUnlock(global);

        // On success the clipboard owns the memory; on failure it is still ours to free.
        if (PInvoke.SetClipboardData(format, new HANDLE(global.Value)).IsNull)
        {
            PInvoke.GlobalFree(global);
            throw new IOException($"The clipboard rejected format {format}.");
        }
    }

    private static uint Register(string name) => PInvoke.RegisterClipboardFormat(name);

    private static byte[] EncodeUtf16(string text)
    {
        var bytes = new byte[(text.Length + 1) * sizeof(char)];
        Encoding.Unicode.GetBytes(text, bytes);
        return bytes;
    }

    private static string DecodeUtf16(byte[] bytes)
    {
        var chars = MemoryMarshal.Cast<byte, char>(bytes.AsSpan(0, bytes.Length & ~1));
        var end = chars.IndexOf('\0');
        return new string(end >= 0 ? chars[..end] : chars);
    }

    private static byte[] WithTerminator(byte[] bytes)
    {
        var result = new byte[bytes.Length + 1];
        bytes.CopyTo(result, 0);
        return result;
    }

    private readonly struct ClipboardScope : IDisposable
    {
        // Clipboard managers hold the clipboard briefly after every change, so a failed open is retried.
        public static ClipboardScope Open()
        {
            for (var attempt = 1; ; attempt++)
            {
                if (PInvoke.OpenClipboard(HWND.Null))
                    return default;
                if (attempt == OpenAttempts)
                    throw new IOException("The clipboard is in use by another application.");
                Thread.Sleep(10);
            }
        }

        public void Dispose() => PInvoke.CloseClipboard();
    }
}
